﻿#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using MagicStorage.Sorting;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace MagicStorage.Common.Systems;

public class MagicCache : ModSystem
{
	public static Recipe[] EnabledRecipes { get; private set; } = null!;
	public static Dictionary<int, Recipe[]> ResultToRecipe { get; private set; } = null!;
	public static Dictionary<FilterMode, Recipe[]> FilteredRecipesCache { get; private set; } = null!;

	public static Dictionary<int, List<Recipe>> hasIngredient { get; private set; } = null!;
	public static Dictionary<int, List<Recipe>> hasTile { get; private set; } = null!;

	public static Mod[] AllMods { get; private set; } = null!;
	public static Dictionary<Mod, int> IndexByMod { get; private set; } = null!;
	public static Dictionary<Mod, Recipe[]> RecipesByMod { get; private set; } = null!;
	public static Recipe[] VanillaRecipes { get; private set; } = null!;

	public static Dictionary<int, Recipe[]> RecipesUsingItemType { get; private set; } = null!;

	public override void Unload()
	{
		EnabledRecipes = null!;
		ResultToRecipe = null!;
		FilteredRecipesCache = null!;

		hasIngredient = null!;
		hasTile = null!;

		AllMods = null!;
		IndexByMod = null!;
		RecipesByMod = null!;
		VanillaRecipes = null!;

		RecipesUsingItemType = null!;
	}

	public override void PostSetupContent()
	{
		SortingCache.dictionary.Fill();
	}

	public override void PostSetupRecipes()
	{
		EnabledRecipes = Main.recipe.Take(Recipe.numRecipes).Where(r => !r.Disabled).ToArray();
		ResultToRecipe = EnabledRecipes.GroupBy(r => r.createItem.type).ToDictionary(x => x.Key, x => x.ToArray());

		hasIngredient = new();
		hasTile = new();

		//Initialize the lookup tables
		foreach (var recipe in EnabledRecipes)
		{
			foreach (var item in recipe.requiredItem)
			{
				if (!hasIngredient.TryGetValue(item.type, out var list))
					hasIngredient[item.type] = list = new();

				list.Add(recipe);
			}

			foreach (var tile in recipe.requiredTile)
			{
				if (!hasTile.TryGetValue(tile, out var list))
					hasTile[tile] = list = new();

				list.Add(recipe);
			}
		}

		SetupSortFilterRecipeCache();

		var groupedByMod = EnabledRecipes.GroupBy(r => r.Mod).ToArray();
		RecipesByMod = groupedByMod.Where(x => x.Key is not null).ToDictionary(x => x.Key, x => x.ToArray());
		VanillaRecipes = groupedByMod.Where(x => x.Key is null).SelectMany(x => x.ToArray()).ToArray();

		// TODO: Split into mods with recipe and mods with items. Also have to account for it in ModSearchBox
		AllMods = ModLoader.Mods
			.Where(mod => RecipesByMod.ContainsKey(mod) || mod.GetContent<ModItem>().Any())
			.ToArray();

		IndexByMod = AllMods
			.Select((mod, index) => (mod, index))
			.ToDictionary(x => x.mod, x => x.index);

		RecipesUsingItemType = ContentSamples.ItemsByType.Where(kvp => !kvp.Value.IsAir)
			.ToDictionary(kvp => kvp.Key, kvp => EnabledRecipes.Where(r => r.createItem.type == kvp.Key || r.requiredItem.Any(i => i.type == kvp.Key))
				.ToArray());
	}

	private static void SetupSortFilterRecipeCache()
	{
		FilteredRecipesCache = new();

		foreach (var filterMode in Enum.GetValues<FilterMode>())
		{
			if (filterMode is FilterMode.Recent)
				continue;

			var filter = ItemSorter.GetFilter(filterMode);

			var recipes = EnabledRecipes.Where(r => filter(r.createItem));

			FilteredRecipesCache[filterMode] = recipes.ToArray();
		}
	}
}
