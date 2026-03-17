// Stub: overridden by ChannelAnchor.g.cs during payload builds.
// Provides empty channel list for direct (non-builder) builds.
namespace Workflow.Config
{
    internal static partial class _ChannelRef
    {
        static partial void _GetImpl(ref System.Type[]? types);

        internal static System.Type[] _Get()
        {
            System.Type[]? types = null;
            _GetImpl(ref types);
            return types ?? System.Array.Empty<System.Type>();
        }
    }
}
