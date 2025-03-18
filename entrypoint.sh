#!/bin/bash

# re-entrant script to support automatically switching to an unprivileged user
# that matches the ownership of the RUN_WORKDIR (see below)

set -eu
shopt -s nullglob inherit_errexit

error_trap() {
  local el=${1:=??} ec=${2:=??} lc="$BASH_COMMAND"
  echo >&2 "ERROR in $(basename $0) : line $el error $ec : $lc"
  exit ${2:=1}
}
trap 'error_trap ${LINENO} ${?}' ERR

RUN_USER=build
RUN_WORKDIR="${PWD}"

ARGS=("$@")
if [ "${#ARGS[@]}" -eq 0 ] ; then
    ARGS+=("/bin/bash")
fi

[ -z "${UID:-}" ] && UID=$(id -u)
[ -z "${GID:-}" ] && GID=$(id -g)

[ "$UID" -ne 0 ] && RUNNING_NON_ROOT=1

# check if required path is mounted
if ! grep -sq " ${RUN_WORKDIR} " < /proc/mounts ; then
  echo >&2 "error: ${RUN_WORKDIR} is not mounted in the container." ; exit 1
fi

create_user() {
  if [ "$1" -gt 0 ] ; then
    if [ "$2" -gt 0 ] ; then
      su - -c "groupadd -g $2 $RUN_USER" 2>/dev/null || true
    fi
    su - -c "useradd -m -d $3 -u $1 -g $2 $RUN_USER ; passwd -d $RUN_USER >/dev/null"
  fi
}

# skip if re-running under newly created user
if [ -z "${ENTRYPOINT_RELOAD:-}" ] ; then
  if [ -z "${RUNNING_NON_ROOT:-}" ] ;  then
    RUN_UID=$(stat -c '%u' "$RUN_WORKDIR")
    RUN_GID=$(stat -c '%g' "$RUN_WORKDIR")
    [ "$RUN_UID" -eq 0 ] && RUN_USER="root"
  fi
  create_user "$RUN_UID" "$RUN_GID" "/home/${RUN_USER}"
  # copy dotnet cache to new user
  cp -r /root/.dotnet /root/.nuget /root/.cache /root/.local "/home/${RUN_USER}/"
  chown -R "${RUN_USER}:" "/home/${RUN_USER}"
  # re-run with new user
  export HOME=$(getent passwd $RUN_USER | cut -d: -f6)
  export ENTRYPOINT_RELOAD=1
  exec runuser -m -P -g $RUN_USER -u $RUN_USER -- "$0" "${ARGS[@]}"
  exit
fi

# sanity check supported volumes
for volume in ${RUN_WORKDIR} ; do
  if [ ! -w "$volume" ] ; then
    echo >&2 "error: unable to write to ${volume}. Ensure permissions are correct on the host." ; exit 1
  fi
  if ! find "$volume/." -maxdepth 1 -name '.' \( -uid "$UID" -a -perm -u+rw \) -o \( -group "$GID" -a -perm -g+rw \) -exec true {} + ; then
    echo >&2 "warning: inconsistent user/group ownership or permissions on ${volume}."
  fi
done

dotnet tool restore

"${ARGS[@]}"
