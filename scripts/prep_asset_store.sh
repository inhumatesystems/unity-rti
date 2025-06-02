#!/bin/bash

cd "$(dirname $0)/.."
cd Packages/com.inhumatesystems.rti/Plugins
rm -rf NaughtyAttributes
git clone -b upm https://github.com/dbrizov/NaughtyAttributes.git
rm -rf NaughtyAttributes/.git
rm -rf NaughtyAttributes/Documentation~
rm -rf NaughtyAttributes/Samples
rm -rf NaughtyAttributes/Samples.meta
rm -rf NaughtyAttributes/Scripts/Test
rm -rf NaughtyAttributes/Scripts/Test.meta
cd - >/dev/null 2>&1
cat Packages/com.inhumatesystems.rti/package.json | grep -v naughtyattributes > Packages/com.inhumatesystems.rti/package.json.tmp
mv Packages/com.inhumatesystems.rti/package.json.tmp Packages/com.inhumatesystems.rti/package.json
cat Packages/manifest.json | grep -v naughtyattributes > Packages/manifest.json.tmp
mv Packages/manifest.json.tmp Packages/manifest.json

echo -e "\nOk now do your thing and don't commit/push\n"
