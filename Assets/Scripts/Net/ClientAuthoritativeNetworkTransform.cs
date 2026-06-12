using Unity.Netcode.Components;

namespace Moba
{
    /// NetworkTransform where the owning client drives the transform (used for heroes).
    public class ClientAuthoritativeNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
