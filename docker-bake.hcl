variable "RELEASE" {
  default = "dev"
}

variable "BEPINEX_RELEASE" {
  default = "5.4.23.3"
}

variable "DOTNET_VERSION" {
  default = "9.0"
}

target "default" {
  dockerfile = "Dockerfile"
  target = "build"
  args = {
    BEPINEX_RELEASE = "${BEPINEX_RELEASE}"
    DOTNET_VERSION = "${DOTNET_VERSION}"
  }
  tags = [
    "valheim-mod-builder:${DOTNET_VERSION}-${RELEASE}"
  ]
  platforms = [
    "linux/amd64"
  ]
}
