﻿using System;
using System.Collections.Generic;
using System.Linq;
using MagicStorage.Sorting;
using Terraria;
using Terraria.ModLoader;

#nullable enable

namespace MagicStorage;

public class MagicCache : ModSystem
{
	public static Recipe[] EnabledRecipes { get; private set; } = null!;
	public static Dictionary<int, Recipe[]> ResultToRecipe { get; private set; } = null!;
	public static Dictionary<FilterMode, Recipe[]> FilteredRecipesCache { get; private set; } = null!;
	public static Dictionary<int, List<Recipe>> hasIngredient { get; private set; } = null!;
	public static Dictionary<int, List<Recipe>> hasTile { get; private set; } = null!;
	public static Dictionary<int, Func<Item, Item, bool>> canCombineByType { get; private set; } = null!;

	public static bool CanCombine(Item item1, Item item2) => ItemData.Matches(item1, item2) && (!canCombineByType.TryGetValue(item1.type, out var func) || func(item1, item2));

	internal static bool CanCombineIgnoreType(Item item1, Item item2) => !canCombineByType.TryGetValue(item1.type, out var func) || func(item1, item2);

	public override void Load()
	{
		canCombineByType = new();
	}

	public override void Unload()
	{
		EnabledRecipes = null!;
		ResultToRecipe = null!;
		FilteredRecipesCache = null!;
		hasIngredient = null!;
		hasTile = null!;
		canCombineByType = null!;
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