# Quotations Realtime (SSE)

API em .NET 10 que transmite cotações em tempo real para o browser via **Server-Sent Events**
(SSE nativo do ASP.NET Core, sem SignalR). As cotações são geradas por um projeto console separado
(`Producer`) e distribuídas através do **RabbitMQ**; a API (`Api`) só consome e retransmite via SSE
— não publica nada. O tráfego de entrada é balanceado por um **Traefik** na frente de duas réplicas
da API.

## Arquitetura

```mermaid
flowchart TB
    browser["Browser<br/>EventSource (SSE)"]

    producer["producer<br/>QuotationProducerService"]

    subgraph traefik["Traefik — load_balancer · :8080"]
        direction TB
        router["Router: PathPrefix(/quotations-realtime-sse)"]
        mw["strip-prefix → compress → rate-limit → circuit-breaker"]
    end

    subgraph api1["api — réplica 1"]
        direction TB
        ep1["QuotationEndpoints<br/>GET /quotations/realtime"]
        bc1["QuotationBroadcaster<br/>(singleton, por processo)"]
        cons1["QuotationConsumerService"]
        cons1 -->|"Publish(quotation)"| bc1
        bc1 -->|"fan-out em memória"| ep1
    end

    subgraph api2["api — réplica 2"]
        direction TB
        ep2["QuotationEndpoints<br/>GET /quotations/realtime"]
        bc2["QuotationBroadcaster<br/>(singleton, por processo)"]
        cons2["QuotationConsumerService"]
        cons2 -->|"Publish(quotation)"| bc2
        bc2 -->|"fan-out em memória"| ep2
    end

    subgraph rabbitmq["RabbitMQ"]
        direction TB
        exch{{"exchange: quotations.broadcast (fanout, durable)"}}
        q1[["fila anônima — réplica 1<br/>exclusive · autoDelete"]]
        q2[["fila anônima — réplica 2<br/>exclusive · autoDelete"]]
        exch -->|fanout| q1
        exch -->|fanout| q2
    end

    browser -->|"GET /quotations-realtime-sse/quotations/realtime"| traefik
    traefik -->|round-robin| api1
    traefik -->|round-robin| api2

    producer -->|"publish (routingKey vazio)"| exch
    q1 -->|consume| cons1
    q2 -->|consume| cons2
```

**Por que fanout em vez de fila compartilhada:** o `QuotationBroadcaster` vive em memória, isolado
por processo — cada réplica só sabe distribuir eventos aos clientes SSE conectados a ela mesma. Se
as duas réplicas consumissem a **mesma fila nomeada**, o RabbitMQ dividiria as mensagens entre elas
(competing consumers) e cada cliente veria só uma fatia do stream. Com uma exchange `fanout` e uma
fila exclusiva por réplica, toda mensagem publicada é copiada para todas as réplicas vivas, então
qualquer cliente SSE vê o mesmo stream completo, não importa a qual réplica o Traefik o conectou.

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/) (para rodar localmente sem Docker)
- Docker + Docker Compose (para subir o stack completo)
- Um RabbitMQ acessível (local, container avulso ou remoto)

## Configuração

As opções do RabbitMQ ficam na seção `RabbitMq` de `Api/appsettings.json` (consumer) e
`Producer/appsettings.json` (producer) — mesmo formato nos dois — e podem ser sobrescritas por
variáveis de ambiente (`RabbitMq__HostName`, etc.) sem editar o arquivo:

| Chave          | Descrição                                              | Valor de exemplo       |
|----------------|---------------------------------------------------------|-------------------------|
| `HostName`     | Host do broker RabbitMQ                                 | `10.0.0.150`            |
| `VirtualHost`  | Virtual host usado                                       | `quotation`             |
| `ExchangeName` | Exchange `fanout` onde as cotações são publicadas        | `quotations.broadcast`  |
| `UserName`     | Usuário do RabbitMQ                                      | `guest`                 |
| `Password`     | Senha do RabbitMQ                                        | `guest`                 |

> O `appsettings.json` do repositório traz um host interno e as credenciais padrão do RabbitMQ
> apenas para desenvolvimento local. Ajuste via variável de ambiente para outros ambientes.

## Como subir

### Opção A — Docker Compose (Traefik + 2 réplicas da API + producer)

```bash
docker compose up --build -d
docker compose ps
```

Isso sobe:
- `loadbalancer` (Traefik) expondo `http://localhost:8080`, roteando `PathPrefix(/quotations-realtime-sse)` para o serviço `api`;
- `api` com 2 réplicas (`deploy.replicas: 2`), só consumindo do RabbitMQ e retransmitindo via SSE;
- `producer`, que gera cotações fake e as publica no RabbitMQ a cada 750ms.

> **Atenção:** o healthcheck do serviço `api` no `docker-compose.yml` aponta para
> `/health/live?source=docker`, mas a aplicação só expõe `/health` (ver `Program.cs`). Enquanto
> essa divergência não for corrigida, o container `api` não atinge o estado `healthy` e o
> `loadbalancer` — que depende de `api: condition: service_healthy` — não chega a subir. Ajuste o
> `test:` do healthcheck para `/health` (mesmo caminho já usado no label do Traefik) antes de usar
> em qualquer ambiente real.

### Opção B — Localmente com `dotnet run` (uma única instância)

Sem o `producer` rodando, a API não recebe nenhuma cotação — suba os dois, em terminais separados:

```bash
cd Api
dotnet run
```

```bash
cd Producer
dotnet run
```

A API sobe em `http://localhost:7000` (perfil `http` de `Properties/launchSettings.json`), sem
Traefik e sem múltiplas réplicas — útil para desenvolvimento e depuração. O `Producer` não expõe
porta nenhuma, só publica no RabbitMQ.

## Como usar

Abra `ui/index.html` diretamente no browser. Ele conecta via `EventSource` em:

```
http://localhost:8080/quotations-realtime-sse/quotations/realtime
```

(URL fixa, presumindo o stack subido pela Opção A). Cada cotação recebida atualiza um card por
`tickerId` na tela.

Se estiver rodando pela Opção B (sem Traefik), ajuste a URL no `ui/index.html` para
`http://localhost:7000/quotations/realtime`.

## Como chamar a API diretamente

O endpoint expõe um stream SSE nomeado `quotations`:

```
GET /quotations/realtime            # direto na API (Opção B)
GET /quotations-realtime-sse/quotations/realtime   # através do Traefik (Opção A)
```

Cada evento traz um JSON no formato:

```json
{ "tickerId": "PETR4", "value": 4.32 }
```

**Via curl** (mantém a conexão aberta e imprime cada evento conforme chega):

```bash
curl -N http://localhost:8080/quotations-realtime-sse/quotations/realtime
```

**Via JavaScript**:

```js
const eventSource = new EventSource(
  "http://localhost:8080/quotations-realtime-sse/quotations/realtime"
);

eventSource.addEventListener("quotations", (event) => {
  const { tickerId, value } = JSON.parse(event.data);
  console.log(tickerId, value);
});
```