FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet
FROM node:24-bookworm-slim AS node
FROM jenkins/jenkins:lts-jdk21
USER root
RUN apt-get update && apt-get install -y --no-install-recommends libicu-dev libssl-dev zlib1g ca-certificates \
    && rm -rf /var/lib/apt/lists/*
COPY --from=dotnet /usr/share/dotnet /usr/share/dotnet
COPY --from=node /usr/local/ /usr/local/
RUN ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet
ENV DOTNET_ROOT=/usr/share/dotnet \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1
COPY plugins.txt /usr/share/jenkins/ref/plugins.txt
RUN jenkins-plugin-cli --plugin-file /usr/share/jenkins/ref/plugins.txt
COPY init.groovy /usr/share/jenkins/ref/init.groovy.d/lab.groovy
USER jenkins
