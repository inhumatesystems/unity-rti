# Inhumate RTI Integration Package for Unity

Package for integrating your Unity-based simulator or application with the RTI 
(Runtime Infrastructure), part of Inhumate Suite.

For more information, see https://inhumatesystems.com/products/suite/

## Dependencies

This package depends on NaughtyAttributes, available via
- Unity Asset Store - https://assetstore.unity.com/packages/tools/utilities/naughtyattributes-129996
- OpenUPM - https://openupm.com/packages/com.dbrizov.naughtyattributes/

## Quick Start

To connect your simulator/application to the Inhumate RTI:

1. Add an empty GameObject to your scene
2. Add the `RTI Connection` component
3. Press play, your application should now be visible in the RTI Control Panel

To publish a dynamic entity on the RTI (e.g. your player GameObject):

1. Add the `RTI Entity` component to a GameObject in the scene
2. Add the `RTI Position` component
3. Press play, the object (and its location) should be visible as an entity in Inhumate Viewer 3D tab

To publish import static objects as geometry:

1. Add the `RTI Static Geometry` component
2. Press play, the object should be visible as geometry in the Inhumate Viewer 3D tab

For more advanced topics, read more in the documentation at 
https://docs.inhumatesystems.com/integrations/unity/

