using Content.Shared.FixedPoint;

namespace Content.Server.Backmen.Blob;

[RegisterComponent]
public sealed partial class BlobResourceComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("pointsPerPulsed")]
    public FixedPoint2 PointsPerPulsed = 3;
}
