FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG VERSION=0.1.0
WORKDIR /src

COPY ./ /src
RUN dotnet publish -c Release -r linux-musl-x64 -p:Version=$VERSION

FROM alpine:latest
WORKDIR /src

COPY --from=build /src/src/bin/Release/net8.0/linux-musl-x64/publish/ /src
RUN apk upgrade --update-cache --available && apk add openssl icu-libs libsodium libsodium-dev && rm -rf /var/cache/apk/*

ENTRYPOINT /src/CookieClicker