﻿using SPTarkov.Server.Core.Models.Spt.Config;

namespace SPTarkov.Server.Core.Extensions
{
    /// <summary>
    /// Get the rouble amount for the desired container, multiplied by the current map bot will spawn on
    /// </summary>
    public static class LootContainerSettingsExtensions
    {
        public static double GetRoubleValue(
            this LootContainerSettings settings,
            int botLevel,
            string locationId
        )
        {
            var roubleTotalByLevel = GetContainerRoubleTotalByLevel(
                botLevel,
                settings.TotalRubByLevel
            );

            // Get multiplier for map, use default if map not found
            if (!settings.LocationMultipler.TryGetValue(locationId, out var multiplier))
            {
                settings.LocationMultipler.TryGetValue("default", out multiplier);
            }

            return roubleTotalByLevel * multiplier;
        }

        /// <summary>
        ///     Gets the rouble cost total for loot in a bots backpack by the bots level
        ///     Will return 0 for non PMCs
        /// </summary>
        /// <param name="botLevel">level of the bot</param>
        /// <param name="containerLootValuesPool">Pocket/vest/backpack</param>
        /// <returns>rouble amount</returns>
        private static double GetContainerRoubleTotalByLevel(
            int botLevel,
            List<MinMaxLootValue> containerLootValuesPool
        )
        {
            var matchingValue = containerLootValuesPool.FirstOrDefault(minMaxValue =>
                botLevel >= minMaxValue.Min && botLevel <= minMaxValue.Max
            );

            if (matchingValue is null)
            {
                return 1;
            }

            return matchingValue.Value;
        }
    }
}
