// SPDX-License-Identifier: AGPL-3.0-or-later
// Adapted from Misfit-Sanctuary/nuclear-14 @ <source-commit> (AGPL-3.0). See CONTRIBUTING.md §5.
using Content.Shared._Zona14.WorldMap;
using Robust.Shared.Utility;

namespace Content.Server._Zona14.WorldMap;

/// <summary>
/// Backing data for the PDA map program. Annotations are shared: everyone reading the same
/// cartridge sees the same notes.
/// </summary>
[RegisterComponent]
public sealed partial class WorldMapCartridgeComponent : Component
{
    [DataField(required: true)]
    public ResPath MapTexturePath = default!;

    [DataField]
    public string MapTitle = "Map";

    [DataField(required: true)]
    public Box2 WorldBounds;

    [DataField]
    public List<WorldMapAnnotation> Annotations = new();
}
