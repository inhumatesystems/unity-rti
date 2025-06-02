# Inhumate RTI Integration Package for Unity

Package for integrating your Unity-based simulator or application with Inhumate RTI (Runtime Infrastructure), part of [Inhumate Suite](https://inhumatesystems.com/products/suite/).

Read more in the [documentation](https://docs.inhumatesystems.com/integrations/unity/).

## Installation

### OpenUPM

A simple way to install the package is using the OpenUPM [command-line tool](https://openupm.com/packages/com.inhumatesystems.rti/#modal-commandlinetool):

```sh
openupm add com.inhumatesystems.rti
```

If you don't feel comfortable with the command-line tool you can also [manually install](https://openupm.com/packages/com.inhumatesystems.rti/#modal-manualinstallation) from OpenUPM using the package manager in Unity.

### Install from tarball

You can install this package from a tarball (`.tgz` file) [downloaded from Inhumate](https://get.inhumatesystems.com/product/unity-rti).

See the section [Installing a UPM package from a local tarball file](https://docs.unity3d.com/2022.3/Documentation/Manual/upm-ui-tarball.html) from the Unity manual for instructions.

### Dependencies

This package uses the brilliant NaughtyAttributes package. 
If you install via OpenUPM it should be solved automatically.
For local tarball or asset store version, the dependency is bundled.

If you need to, you can get NaughtyAttributes from [OpenUPM](https://openupm.com/packages/com.dbrizov.naughtyattributes/) or [Unity Asset Store](https://assetstore.unity.com/packages/tools/utilities/naughtyattributes-129996), or directly from github, by adding this line to your `manifest.json` dependencies:

```json
    "com.dbrizov.naughtyattributes": "https://github.com/dbrizov/NaughtyAttributes.git#upm",
```

## Development

### CI Build

The CI build is based on the fantastic work of Gabriel Le Breton and his docker images.
See https://gitlab.com/gableroux/unity3d-gitlab-ci-example.

To upgrade to a new Unity version, you need to update the license Gitlab CI uses.
Please see https://gitlab.com/gableroux/unity3d-gitlab-ci-example#b-locally for instructions.
