# Inhumate RTI Integration Package for Unity

Package for integrating your Unity-based simulator or application with the RTI (Runtime Infrastructure) of [Inhumate Suite](https://inhumatesystems.com/products/suite/).

Read more in the [documentation](https://docs.inhumatesystems.com/integrations/unity/).

## Dependencies

This package uses the brilliant NaughtyAttributes package. Add this line to your `manifest.json` dependencies:

```json
    "com.dbrizov.naughtyattributes": "https://github.com/dbrizov/NaughtyAttributes.git#upm",
```

Or OpenUPM registry:

```json
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.dbrizov.naughtyattributes",
        "com.openupm"
      ]
    }
  ],
```

## Usage from tarball

You can use this package from a local tarball (`.tgz` file).

See the section [Installing a package from a local tarball file](https://docs.unity3d.com/2020.3/Documentation/Manual/upm-ui-tarball.html) from the Unity manual.

## Development

### CI Build

The CI build is based on the fantastic work of Gabriel Le Breton and his docker images.
See https://gitlab.com/gableroux/unity3d-gitlab-ci-example.

To upgrade to a new Unity version, you need to update the license Gitlab CI uses.
Please see https://gitlab.com/gableroux/unity3d-gitlab-ci-example#b-locally for instructions.
