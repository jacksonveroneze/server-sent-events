# =====================================================
# Build + Publish
# =====================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS publish

WORKDIR /src

COPY ./Api.csproj ./
COPY *.slnx ./

RUN dotnet restore ./Api.csproj \
    -r linux-x64 \
    -p:PublishReadyToRun=true

COPY . .

RUN dotnet publish ./Api.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    --no-restore \
    -p:PublishReadyToRun=true \
    -p:PublishTrimmed=false \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    --output /publish

# =====================================================
# Etapa Final: Imagem de Runtime
# =====================================================
FROM jacksonveroneze/aspnet:10.0-alpine-1.5.0

ENV DOTNET_GCDynamicAdaptationMode=1 \
    DOTNET_GCHeapHardLimitPercent=0x4B \
    DOTNET_TieredCompilation=1 \
    DOTNET_TC_QuickJitForLoops=1

ENV ASSEMBLY_NAME=/home/app/Api.dll

COPY --from=publish /publish /home/app
