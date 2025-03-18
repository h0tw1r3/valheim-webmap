variable "RELEASE" {
  default = "dev"
}

variable "BEPINEX_RELEASE" {
  default = "5.4.23.2"
}

target "default" {
  dockerfile = "Dockerfile"
  target = "build"
  args = {
    BEPINEX_RELEASE = "${BEPINEX_RELEASE}"
  }
  tags = [
    "valheim-mod-builder:${RELEASE}"
  ]
  platforms = [
    "linux/amd64"
  ]
}

target "init" {
  dockerfile = "Dockerfile"
  target = "init"
  tags = [
    "valheim-mod-builder-init:${RELEASE}"
  ]
  platforms = [
    "linux/amd64"
  ]
}
