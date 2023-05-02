FROM mcr.microsoft.com/dotnet/sdk:8.0-preview-alpine AS build
WORKDIR /src

COPY ./ /src
RUN dotnet publish -c Release -r linux-musl-x64

FROM alpine:latest
WORKDIR /src

COPY --from=build /src/CookieClicker/bin/Release/net8.0/linux-musl-x64/publish /src
RUN apk upgrade --update-cache --available && apk add openssl icu-libs && rm -rf /var/cache/apk/*

ENTRYPOINT /src/CookieClicker