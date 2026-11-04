# syntax=docker/dockerfile:1
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# Restore as a separate layer. Publishing for the target architecture only keeps
# the native ImageMagick libraries of the other platforms out of the image.
COPY IMApi.csproj ./
RUN dotnet restore IMApi.csproj -a $TARGETARCH

COPY . .
RUN dotnet publish IMApi.csproj -c Release -a $TARGETARCH --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# Liberation Sans (metric-compatible with Arial) for the texts in the images
RUN apt-get update \
    && apt-get install -y --no-install-recommends fonts-liberation \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# SQLite database with the image texts. 1000:1000 is the default user of the
# opencloud-compose containers (OC_CONTAINER_UID_GID).
RUN mkdir -p /data && chown 1000:1000 /data
VOLUME /data

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ImApi__DatabasePath=/data/imapi.db \
    ImApi__LabelFont=/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf

EXPOSE 8080
USER 1000:1000
ENTRYPOINT ["dotnet", "IMApi.dll"]
