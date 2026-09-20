ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS dotnet-base

ENV DEBIAN_NONINTERACTIVE=1
ENV PATH="$PATH:./tools:~/.dotnet/tools:/opt/steam"
ENV LANG="C.UTF-8"
ENV TZ="Etc/UTC"
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_NOLOGO=1

SHELL ["/bin/bash", "-exu", "-o", "pipefail", "-c"]

RUN <<EOF
passwd -d root

cat <<EOD >/etc/apt/apt.conf.d/docker-clean
APT::Install-Recommends "0";
APT::Install-Suggests "0";
Acquire::Retries "5";
Dpkg::Use-Pty "0";
Dpkg::Progress-Fancy "0";
Binary::apt::APT::Keep-Downloaded-Packages "true";
APT::Keep-Downloaded-Packages "true";
EOD

EOF

RUN --mount=type=cache,target=/var/cache/apt,sharing=locked \
    --mount=type=cache,target=/var/lib/apt,sharing=locked <<EOD
apt-get update -q
apt-get install -qy unzip vim-tiny lib32gcc-s1 util-linux dumb-init
find /var/log -name '*.log' -delete
EOD

FROM dotnet-base AS dotnet

RUN --mount=type=cache,target=/var/cache/apt,sharing=locked \
    --mount=type=cache,target=/var/lib/apt,sharing=locked \
    apt-get update -q && \
    apt-get install -qy npm webpack && \
    find /var/log -name '*.log' -delete

# 6.0 runtime is currently required for BepInEx Assembly Publicizer Cli
RUN /usr/lib/apt/apt-helper download-file https://dot.net/v1/dotnet-install.sh /usr/local/bin/dotnet-install.sh && \
    chmod +x /usr/local/bin/dotnet-install.sh && \
    dotnet-install.sh -c 6.0 -i /usr/share/dotnet --runtime dotnet && \
    dotnet workload update && \
    rm -rf /tmp/*

ARG BEPINEX_RELEASE=5.4.23.5
FROM dotnet-base AS steam

RUN <<EOF
groupadd -g 500 steam
useradd -m -d /opt/steam -u 500 -g 500 steam
passwd -d steam >/dev/null
EOF

USER steam
WORKDIR /opt/steam

RUN --mount=type=cache,target=/cache,mode=0777 <<EOF
if ! test -s /cache/steamcmd_linux.tar.gz ; then
    curl -L -o /cache/steamcmd_linux.tar.gz https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz
fi
tar zxvf /cache/steamcmd_linux.tar.gz
ln -s ~/steamcmd.sh ~/steamcmd
steamcmd +login anonymous +quit
EOF

FROM steam AS game

RUN <<EOF
steamcmd +force_install_dir "/opt/steam/valheim" +login anonymous +app_update 896660 +quit
EOF

FROM dotnet AS build

RUN --mount=type=cache,target=/cache,mode=0777 <<EOF
if ! test -s /cache/bepinex-${BEPINEX_RELEASE}.zip ; then
    /usr/lib/apt/apt-helper download-file https://github.com/BepInEx/BepInEx/releases/download/v${BEPINEX_RELEASE}/BepInEx_win_x64_${BEPINEX_RELEASE}.zip /cache/bepinex-${BEPINEX_RELEASE}.zip
fi
unzip /cache/bepinex-${BEPINEX_RELEASE}.zip BepInEx/*
mv BepInEx /usr/local/share/BepInEx-${BEPINEX_RELEASE}
ln -s /usr/local/share/BepInEx-${BEPINEX_RELEASE} /opt/BepInEx
EOF

COPY --from=game /opt/steam/valheim/valheim_server_Data/Managed /opt/steam/libs

USER root
WORKDIR /build

RUN chmod a+rx /root

COPY docker/entrypoint.sh /.entrypoint.sh

ENTRYPOINT ["/.entrypoint.sh"]
