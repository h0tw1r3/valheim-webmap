variable "BEPINEX_VALHEIM_RELEASE" {
  default = "5.4.2351"
}

variable "DOTNET_VERSION" {
  default = "10.0"
}

target "default" {
  dockerfile = "Dockerfile"
  target = "build"
  args = {
    BEPINEX_VALHEIM_RELEASE = "${BEPINEX_VALHEIM_RELEASE}"
    DOTNET_VERSION = "${DOTNET_VERSION}"
  }
  tags = [
    "valheim-build:${BEPINEX_VALHEIM_RELEASE}",
    "valheim-build:latest"
  ]
  platforms = [
    "linux/amd64"
  ]
}

target "server" {
  dockerfile = "Dockerfile"
  target = "game"
  args = {
    BEPINEX_VALHEIM_RELEASE = "${BEPINEX_VALHEIM_RELEASE}"
  }
  tags = [
    "valheim-server:${BEPINEX_VALHEIM_RELEASE}",
    "valheim-server:latest"
  ]
  platforms = [
    "linux/amd64"
  ]
}
