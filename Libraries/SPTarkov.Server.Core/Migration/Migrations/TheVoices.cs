﻿using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Services;

namespace SPTarkov.Server.Core.Migration.Migrations
{
    /// <summary>
    /// In 16.8.0.37972 BSG added customization for voices, technically this only affects BE profiles, but this should fix these.
    /// </summary>
    [Injectable]
    public class TheVoices(DatabaseService databaseService) : AbstractProfileMigration
    {
        private bool _pmcVoiceIsMissing = false;
        private bool _scavVoiceIsMissing = false;

        public override string FromVersion
        {
            get { return "~4.0"; }
        }

        public override string ToVersion
        {
            get { return "~4.0"; }
        }

        public override string MigrationName
        {
            get { return "TheVoices400"; }
        }

        public override IEnumerable<Type> PrerequisiteMigrations
        {
            // Requires ThreeTenToThreeEleven on legacy profiles, due to that changing customization for the first time
            get { return [typeof(ThreeTenToThreeEleven)]; }
        }

        public override bool CanMigrate(
            JsonObject profile,
            IEnumerable<IProfileMigration> previouslyRanMigrations
        )
        {
            _pmcVoiceIsMissing = profile["characters"]?["pmc"]?["Customization"]?["Voice"] == null;

            _scavVoiceIsMissing =
                profile["characters"]?["scav"]?["Customization"]?["Voice"] == null;

            return _pmcVoiceIsMissing || _scavVoiceIsMissing;
        }

        public override JsonObject? Migrate(JsonObject profile)
        {
            if (_pmcVoiceIsMissing)
            {
                HandlePmcVoice(profile);
            }

            if (_scavVoiceIsMissing)
            {
                HandleScavVoice(profile);
            }

            return profile;
        }

        private void HandlePmcVoice(JsonObject profileObject)
        {
            var pmcInfo = profileObject["characters"]!["pmc"]!["Info"] as JsonObject;

            var oldVoice = pmcInfo["Voice"]?.ToString() ?? "";
            pmcInfo.Remove("Voice");

            var voiceMongoId = databaseService
                .GetCustomization()
                .FirstOrDefault(x => x.Value.Properties.Name == oldVoice)
                .Key;

            profileObject["characters"]!["pmc"]!["Customization"]!["Voice"] =
                voiceMongoId.ToString();
        }

        private void HandleScavVoice(JsonObject profileObject)
        {
            var pmcInfo = profileObject["characters"]!["scav"]!["Info"] as JsonObject;

            var oldVoice = pmcInfo["Voice"]?.ToString() ?? "";
            pmcInfo.Remove("Voice");

            var voiceMongoId = databaseService
                .GetCustomization()
                .FirstOrDefault(x => x.Value.Properties.Name == oldVoice)
                .Key;

            profileObject["characters"]!["scav"]!["Customization"]!["Voice"] =
                voiceMongoId.ToString();
        }
    }
}
