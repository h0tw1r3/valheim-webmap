variable "RELEASE" {
  default = "dev"
}

variable "BEPINEX_RELEASE" {
  default = "5.4.23.5"
}

variable "DOTNET_VERSION" {
  default = "10.0"
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
