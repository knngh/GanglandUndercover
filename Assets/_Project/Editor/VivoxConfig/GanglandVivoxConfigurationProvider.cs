using Unity.Services.Core.Configuration.Editor;
using UnityEditor.Build;

namespace GanglandUndercover.Editor
{
    internal sealed class GanglandVivoxConfigurationProvider : IConfigurationProvider
    {
        private const string PlaceholderVivoxServer = "https://placeholder.vivox.invalid/api2";
        private const string PlaceholderVivoxDomain = "gangland-placeholder";
        private const string PlaceholderVivoxIssuer = "gangland-placeholder";
        private const string PlaceholderVivoxTokenKey = "gangland-placeholder";

        private const string ServerKey = "com.unity.services.vivox.server";
        private const string DomainKey = "com.unity.services.vivox.domain";
        private const string IssuerKey = "com.unity.services.vivox.issuer";
        private const string TokenKey = "com.unity.services.vivox.token";
        private const string EnvironmentCustomKey = "com.unity.services.vivox.is-environment-custom";
        private const string TestModeKey = "com.unity.services.vivox.is-test-mode";

        int IOrderedCallback.callbackOrder => 1000;

        public void OnBuildingConfiguration(ConfigurationBuilder builder)
        {
            if (HasConfiguredVivox(builder))
            {
                return;
            }

            builder.SetString(ServerKey, PlaceholderVivoxServer);
            builder.SetString(DomainKey, PlaceholderVivoxDomain);
            builder.SetString(IssuerKey, PlaceholderVivoxIssuer);
            builder.SetString(TokenKey, PlaceholderVivoxTokenKey);
            builder.SetBool(EnvironmentCustomKey, true);
            builder.SetBool(TestModeKey, false);
        }

        private static bool HasConfiguredVivox(ConfigurationBuilder builder)
        {
            return builder.TryGetString(ServerKey, out string server)
                && builder.TryGetString(DomainKey, out string domain)
                && builder.TryGetString(IssuerKey, out string issuer)
                && !string.IsNullOrWhiteSpace(server)
                && !string.IsNullOrWhiteSpace(domain)
                && !string.IsNullOrWhiteSpace(issuer);
        }
    }
}
