using Content.Client._Zona14.Zona.Pda;
using Content.Client.UserInterface.Fragments;
using Content.Shared._Zona14.Zona;
using Content.Shared._Zona14.Zona.Serialization;
using Content.Shared.CartridgeLoader;
using Content.Shared.CartridgeLoader.Cartridges;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.CartridgeLoader.Cartridges;

// Zona14; The Notekeeper program is the PDA entry point for the Zona world map. i hope no one gets upset about this...
// because Notekeeper is culled from the methods i used    -pierow
public sealed partial class NotekeeperUi : UIFragment
{
    [DataField]
    public ResPath MapTexturePath = new("_Zona14/Zona/Maps/STWorld.png");

    [DataField]
    public string MapTitle = "The Zone";

    [DataField]
    public Box2 WorldBounds = new(-512f, -512f, 512f, 512f);

    private ZonaPdaMapUiFragment? _fragment;
    private readonly List<string> _annotationRecords = new();

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        // Zona14; use the existing server-side Notekeeper string list as compact map records
        _fragment = new ZonaPdaMapUiFragment();
        _fragment.SetMap(MapTitle, MapTexturePath, WorldBounds);
        _fragment.OnAddAnnotation += annotation => SendRecord(
            NotekeeperUiAction.Add,
            ZonaAnnotationCodec.Encode(annotation),
            userInterface);
        _fragment.OnRemoveAnnotation += index => RemoveAnnotation(index, userInterface);
        _fragment.OnClearAnnotations += () => ClearAnnotations(userInterface);
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not NotekeeperUiState notekeeperState)
            return;

        // Zona14; only prefixed records are map annotations. legacy text notes remain untouched
        _annotationRecords.Clear();
        var annotations = new List<ZonaAnnotation>();

        foreach (var record in notekeeperState.Notes)
        {
            if (!ZonaAnnotationCodec.TryDecode(record, out var annotation))
                continue;

            _annotationRecords.Add(record);
            annotations.Add(annotation);
        }

        _fragment?.UpdateAnnotations(annotations.ToArray());
    }

    private void RemoveAnnotation(int index, BoundUserInterface userInterface)
    {
        if (index < 0 || index >= _annotationRecords.Count)
            return;

        SendRecord(NotekeeperUiAction.Remove, _annotationRecords[index], userInterface);
    }

    private void ClearAnnotations(BoundUserInterface userInterface)
    {
        // send exact records so legacy Notekeeper text is never deleted. not like it matters much anyways
        foreach (var record in _annotationRecords.ToArray())
            SendRecord(NotekeeperUiAction.Remove, record, userInterface);
    }

    private static void SendRecord(
        NotekeeperUiAction action,
        string record,
        BoundUserInterface userInterface)
    {
        var notekeeperMessage = new NotekeeperUiMessageEvent(action, record);
        userInterface.SendMessage(new CartridgeUiMessage(notekeeperMessage));
    }
}
