#!/bin/bash

package=com.inhumatesystems.rti

[ -z "$VERSION" ] && VERSION=$CI_COMMIT_TAG
[ -z "$VERSION" -a ! -z "$CI_PIPELINE_IID" ] && VERSION=0.0.$CI_PIPELINE_IID
[ -z "$VERSION" ] && VERSION=0.0.1-dev-version
filename="inhumate-unity-rti-${VERSION}"

cd "$(dirname $0)/.."

unity=$(which Unity)
[ -e "$unity" ] || unity=/opt/Unity/Editor/Unity
[ -e "$unity" ] || unity=/opt/unity/Editor/Unity
[ -e "$unity" ] || unity=$(echo $HOME/Unity/Hub/Editor/2022*/Editor/Unity | awk '{print $NF}')
[ -e "$unity" ] || unity=$(echo /Applications/Unity/Hub/Editor/2022*/Unity.app/Contents/MacOS/Unity | awk '{print $NF}')
[ -e "$unity" ] || unity=$(echo /c/Program\ Files/Unity/Hub/Editor/2022*/Editor/Unity.exe)

if [ ! -e "$unity" -o -z "$unity" ]; then
    echo "uhm... where's unity at?"
    exit 1
fi
echo "Using: $unity"

set -e

"$unity" -logFile -batchmode -nographics -quit -projectPath .

rm -rf Build
mkdir -p Build/package
cp -rfp Packages/$package/* Build/package/

# Remove naughty attributes from package.json - to allow for asset store version or other source...
cat Packages/$package/package.json | grep -v naughtyattributes > Build/package/package.json
# And bundle it for asset store...
cd Build/package/Plugins
git clone -b upm https://github.com/dbrizov/NaughtyAttributes.git
cd ../..

tar cfz $filename.tgz package
