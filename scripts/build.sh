#!/bin/bash

package=com.inhumatesystems.rti

[ -z "$VERSION" ] && VERSION=$CI_COMMIT_TAG
[ -z "$VERSION" -a ! -z "$CI_PIPELINE_IID" ] && VERSION=0.0.$CI_PIPELINE_IID
[ -z "$VERSION" ] && VERSION=0.0.1-dev-version
filename="inhumate-unity-rti-client-${VERSION}"

cd "$(dirname $0)/.."

unity=$(which Unity)
[ -e "$unity" ] || unity=/opt/Unity/Editor/Unity
[ -e "$unity" ] || unity=/opt/unity/Editor/Unity
[ -e "$unity" ] || unity=$(echo $HOME/Unity/Hub/Editor/2022*/Editor/Unity | awk '{print $NF}')

if [ ! -e $unity -o -z "$unity" ]; then
    echo "uhm... where's unity at?"
    exit 1
fi
echo "Using: $unity"

set -e

$unity -logFile -batchmode -nographics -quit -projectPath .

rm -rf Build
mkdir -p Build/package
cp -rfp Packages/$package/* Build/package/
mkdir -p Build/package/Samples~
cp -rfp Assets/* Build/package/Samples~/
cd Build
tar cfz $filename.tgz package
