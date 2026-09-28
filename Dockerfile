ARG DOTNET_VERSION=10.0

FROM ubuntu:24.04 AS game-base

ENV DEBIAN_NONINTERACTIVE=1
ENV PATH="$PATH:./tools:~/.dotnet/tools:/opt/steam"
ENV LANG="C.UTF-8"
ENV TZ="Etc/UTC"

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
apt-get install -qy file libatomic1 libpulse-mainloop-glib0 lib32gcc-s1 libarchive-tools util-linux dumb-init ca-certificates
find /var/log -name '*.log' -delete
EOD

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
apt-get install -qy file libarchive-tools vim-tiny util-linux dumb-init ca-certificates
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

FROM game-base AS steam
SHELL ["/bin/bash", "-exu", "-o", "pipefail", "-c"]

RUN <<EOF
groupadd -g 500 steam
useradd -m -d /opt/steam -u 500 -g 500 steam
passwd -d steam >/dev/null
EOF

USER steam
WORKDIR /opt/steam

RUN --mount=type=cache,target=/cache,mode=0777 <<EOF
if ! test -s /cache/steamcmd_linux.tar.gz ; then
    /usr/lib/apt/apt-helper download-file https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz /cache/steamcmd_linux.tar.gz
fi
tar zxvf /cache/steamcmd_linux.tar.gz
ln -s ~/steamcmd.sh ~/steamcmd
mkdir ~/.steam
ln -sf ~/linux64 ~/.steam/sdk64
ln -sf ~/linux32 ~/.steam/sdk32
steamcmd +login anonymous +quit
EOF

ARG BEPINEX_VALHEIM_RELEASE=5.4.2351
FROM steam AS game

RUN <<EOF
steamcmd +force_install_dir "/opt/steam/valheim" +login anonymous +app_update 896660 +quit
EOF

WORKDIR /opt/steam/valheim

RUN --mount=type=cache,target=/cache,mode=0777 <<EOF
if ! test -s /cache/bepinex-${BEPINEX_VALHEIM_RELEASE}.zip ; then
    /usr/lib/apt/apt-helper download-file https://ccdn.thunderstore.io/live/repository/packages/denikson-BepInExPack_Valheim-${BEPINEX_VALHEIM_RELEASE}.zip /cache/bepinex-${BEPINEX_VALHEIM_RELEASE}.zip
fi
bsdtar -xf /cache/bepinex-${BEPINEX_VALHEIM_RELEASE}.zip --strip-components 1 "BepInExPack_Valheim/*"
chmod +x *_bepinex.sh
EOF

USER root
RUN <<EOF
mkdir /data
chmod g+w /data && chown 500:500 /data
EOF

USER steam
RUN <<EOF
mkdir -p ~/.config/unity3d/IronGate
ln -s /data ~/.config/unity3d/IronGate/Valheim
EOF

VOLUME /data

FROM dotnet AS build

COPY --from=game /opt/steam/valheim/valheim_server_Data/Managed /opt/steam/libs
COPY --from=game /opt/steam/valheim/BepInEx /opt/BepInEx

USER root
WORKDIR /build

RUN chmod a+rx /root

COPY docker/entrypoint-build.sh /.entrypoint.sh

CMD []
ENTRYPOINT ["/.entrypoint.sh"]
