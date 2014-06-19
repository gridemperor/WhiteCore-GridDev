#!/bin/sh
ARCH="x86"
CONFIG="Debug"
BUILD=false

USAGE="[-c <config>] -a <arch>"
LONG_USAGE="Configuration options to pass to prebuild environment

Options:
  -c|--config Build configuration Debug(default) or Release
  -a|--arch Architecture to target x86(default), x64, or AnyCPU
"

while case "$#" in 0) break ;; esac
do
  case "$1" in
    -c|--config)
      shift
      CONFIG="$1"
      ;;
    -a|--arch)
      shift
      ARCH="$1"
      ;;
    -b|--build)
      BUILD=true
      ;;
    -h|--help)
      echo "$USAGE"
      echo "$LONG_USAGE"
      exit
      ;;
    *)
      echo "Illegal option!"
      echo "$USAGE"
      echo "$LONG_USAGE"
      exit
      ;;
  esac
  shift
done

echo Configuring WhiteCore-Sim

mono ./Prebuild.exe /target vs2010 /targetframework v4_0 /conditionals "LINUX;NET_4_0"
if [ -d ".git" ]; then git log --pretty=format:"WhiteCore (%cd.%h)" --date=short -n 1 > WhiteCoreSim/bin/.version; fi

if ${BUILD:=true} ; then
  echo Building WhiteCore-Sim
  xbuild /property:Configuration="$CONFIG" /property:Platform="$ARCH"
  echo Finished Building WhiteCore
  echo Thank you for choosing WhiteCore-Sim
  echo Please report any errors to out Mantis Bug Tracker http://mantis.WhiteCore-sim.org/
fi
