namespace SSAS.Platform.Domain.Enums;

// `OD-SUB-0009` ASKED FOR AN END **OR AN EXPLICIT PERPETUAL MARKER**, AND THE WORD EXPLICIT IS DOING WORK.
//
// A nullable end date alone cannot distinguish *perpetual* from *not yet set*, and the difference decides
// whether a tenant's login is refused. This enum is what makes the marker explicit rather than inferred
// from a null.
public enum SubscriptionTermKind
{
  Fixed = 0,
  Perpetual = 1
}
