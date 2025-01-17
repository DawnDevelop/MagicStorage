using System;
using System.Collections.Generic;
using System.Linq;
using MagicStorage.Components;
using MagicStorage.Sorting;
using MagicStorage.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.UI;

namespace MagicStorage
{
	public static class StorageGUI
	{
		private const int padding = 4;
		private const int numColumns = 10;
		private const float inventoryScale = 0.85f;
		private const int startMaxRightClickTimer = 20;

		public static MouseState curMouse;
		public static MouseState oldMouse;

		private static UIPanel basePanel;
		private static float panelTop;
		private static float panelLeft;
		private static float panelWidth;
		private static float panelHeight;

		private static UIElement topBar;

		// TODO consider Terraria's UISearchBar
		internal static UI.UISearchBar searchBar;
		private static UIButtonChoice sortButtons;
		private static UIToggleButton favoritedOnlyButton;

		internal static UITextPanel<LocalizedText> depositButton;

		private static UIElement topBar2;
		private static UIButtonChoice filterButtons;

		public static readonly ModSearchBox modSearchBox = new(RefreshItems);

		private static readonly UISlotZone slotZone = new(HoverItemSlot, GetItem, inventoryScale);
		private static int slotFocus = -1;

		private static int rightClickTimer;
		private static int maxRightClickTimer = startMaxRightClickTimer;

		internal static UIScrollbar scrollBar = new();
		private static bool scrollBarFocus;
		private static int scrollBarFocusMouseStart;
		private static float scrollBarFocusPositionStart;
		private static readonly float scrollBarViewSize = 1f;
		private static float scrollBarMaxViewSize = 2f;

		private static readonly List<Item> items = new();
		private static readonly List<bool> didMatCheck = new();
		private static int numRows;
		private static int displayRows;

		private static UIElement bottomBar;
		private static UIText capacityText;

		public static bool MouseClicked => curMouse.LeftButton == ButtonState.Pressed && oldMouse.LeftButton == ButtonState.Released;
		public static bool RightMouseClicked => curMouse.RightButton == ButtonState.Pressed && oldMouse.RightButton == ButtonState.Released;

		public static void Initialize()
		{
			InitLangStuff();
			float itemSlotWidth = TextureAssets.InventoryBack.Value.Width * inventoryScale;
			float itemSlotHeight = TextureAssets.InventoryBack.Value.Height * inventoryScale;

			panelTop = Main.instance.invBottom + 60;
			panelLeft = 20f;
			basePanel = new UIPanel();
			float innerPanelWidth = numColumns * (itemSlotWidth + padding) + 20f + padding;
			panelWidth = basePanel.PaddingLeft + innerPanelWidth + basePanel.PaddingRight;
			panelHeight = Main.screenHeight - panelTop - 40f;
			basePanel.Left.Set(panelLeft, 0f);
			basePanel.Top.Set(panelTop, 0f);
			basePanel.Width.Set(panelWidth, 0f);
			basePanel.Height.Set(panelHeight, 0f);
			basePanel.Recalculate();

			topBar = new UIElement();
			topBar.Width.Set(0f, 1f);
			topBar.Height.Set(32f, 0f);
			basePanel.Append(topBar);

			InitSortButtons();
			topBar.Append(sortButtons);

			float x = sortButtons.GetDimensions().Width + 2 * padding;
			favoritedOnlyButton.Left.Set(x, 0f);
			topBar.Append(favoritedOnlyButton);

			// TODO make this a new variable
			x += favoritedOnlyButton.GetDimensions().Width + 2 * padding;

			depositButton.Left.Set(x, 0f);
			depositButton.Width.Set(128f, 0f);
			depositButton.Height.Set(-2 * padding, 1f);
			depositButton.PaddingTop = 8f;
			depositButton.PaddingBottom = 8f;
			topBar.Append(depositButton);

			// TODO also new variable
			x += depositButton.GetDimensions().Width;

			float depositButtonRight = x;
			searchBar.Left.Set(depositButtonRight + padding, 0f);
			searchBar.Width.Set(-depositButtonRight - 2 * padding, 1f);
			searchBar.Height.Set(0f, 1f);
			topBar.Append(searchBar);

			topBar2 = new UIElement();
			topBar2.Width.Set(0f, 1f);
			topBar2.Height.Set(32f, 0f);
			topBar2.Top.Set(36f, 0f);
			basePanel.Append(topBar2);

			InitFilterButtons();

			topBar2.Append(filterButtons);
			float filterButtonsRight = filterButtons.GetDimensions().Width + padding;

			modSearchBox.Button.Left.Set(filterButtonsRight + padding, 0f);
			modSearchBox.Button.Width.Set(-filterButtonsRight - 2 * padding, 1f);
			modSearchBox.Button.Height.Set(0f, 1f);
			modSearchBox.Button.OverflowHidden = true;
			topBar2.Append(modSearchBox.Button);

			slotZone.Width.Set(0f, 1f);
			slotZone.Top.Set(76f, 0f);
			slotZone.Height.Set(-116f, 1f);
			basePanel.Append(slotZone);

			numRows = (items.Count + numColumns - 1) / numColumns;
			displayRows = (int)slotZone.GetDimensions().Height / ((int)itemSlotHeight + padding);
			slotZone.SetDimensions(numColumns, displayRows);
			int noDisplayRows = numRows - displayRows;
			if (noDisplayRows < 0)
				noDisplayRows = 0;
			scrollBarMaxViewSize = 1 + noDisplayRows;
			scrollBar.Height.Set(displayRows * (itemSlotHeight + padding), 0f);
			scrollBar.Left.Set(-20f, 1f);
			scrollBar.SetView(scrollBarViewSize, scrollBarMaxViewSize);
			slotZone.Append(scrollBar);

			bottomBar = new UIElement();
			bottomBar.Width.Set(0f, 1f);
			bottomBar.Height.Set(32f, 0f);
			bottomBar.Top.Set(-32f, 1f);
			basePanel.Append(bottomBar);

			capacityText.Left.Set(6f, 0f);
			capacityText.Top.Set(6f, 0f);
			TEStorageHeart heart = GetHeart();
			int numItems = 0;
			int capacity = 0;
			if (heart is not null)
				foreach (TEAbstractStorageUnit abstractStorageUnit in heart.GetStorageUnits())
					if (abstractStorageUnit is TEStorageUnit storageUnit)
					{
						numItems += storageUnit.NumItems;
						capacity += storageUnit.Capacity;
					}

			capacityText.SetText(Language.GetTextValue("Mods.MagicStorage.Capacity", numItems, capacity));
			bottomBar.Append(capacityText);

			basePanel.Activate();
		}

		private static void InitLangStuff()
		{
			depositButton ??= new UITextPanel<LocalizedText>(Language.GetText("Mods.MagicStorage.DepositAll"));
			searchBar ??= new UI.UISearchBar(Language.GetText("Mods.MagicStorage.SearchName"), RefreshItems);
			modSearchBox.InitLangStuff();
			capacityText ??= new UIText("Items");
		}

		internal static void Unload()
		{
			sortButtons = null;
			filterButtons = null;
			favoritedOnlyButton = null;
		}

		private static void InitSortButtons()
		{
			sortButtons ??= GUIHelpers.MakeSortButtons(RefreshItems);
			favoritedOnlyButton ??= new UIToggleButton(RefreshItems,
				MagicStorage.Instance.Assets.Request<Texture2D>("Assets/FilterMisc", AssetRequestMode.ImmediateLoad),
				Language.GetText("Mods.MagicStorage.ShowOnlyFavorited"));
		}

		private static void InitFilterButtons()
		{
			filterButtons ??= GUIHelpers.MakeFilterButtons(true, RefreshItems);
		}

		public static void Update(GameTime gameTime)
		{
			oldMouse = curMouse;
			curMouse = Mouse.GetState();
			if (Main.playerInventory && StoragePlayer.LocalPlayer.ViewingStorage().X >= 0 && !StoragePlayer.IsStorageCrafting() && !StoragePlayer.IsStorageEnvironment())
			{
				if (curMouse.RightButton == ButtonState.Released)
					ResetSlotFocus();
				basePanel?.Update(gameTime);
				UpdateScrollBar();
				UpdateDepositButton();
				modSearchBox.Update(curMouse, oldMouse);
			}
			else
			{
				scrollBarFocus = false;
				scrollBar.ViewPosition = 0f;
				ResetSlotFocus();
			}
		}

		public static void Draw()
		{
			Player player = Main.LocalPlayer;
			Initialize();
			if (Main.mouseX > panelLeft && Main.mouseX < panelLeft + panelWidth && Main.mouseY > panelTop && Main.mouseY < panelTop + panelHeight)
			{
				player.mouseInterface = true;
				player.cursorItemIconEnabled = false;
				InterfaceHelper.HideItemIconCache();
			}

			basePanel.Draw(Main.spriteBatch);
			slotZone.DrawText();
			sortButtons.DrawText();
			favoritedOnlyButton.DrawText();
			filterButtons.DrawText();
			DrawDepositButton();
		}

		private static Item GetItem(int slot, ref int context)
		{
			int index = slot + numColumns * (int)Math.Round(scrollBar.ViewPosition);
			Item item = index < items.Count ? items[index] : new Item();
			if (!item.IsAir && !didMatCheck[index])
			{
				item.checkMat();
				didMatCheck[index] = true;
			}

			return item;
		}

		private static void UpdateScrollBar()
		{
			if (slotFocus >= 0)
			{
				scrollBarFocus = false;
				return;
			}

			Rectangle dim = scrollBar.GetClippingRectangle(Main.spriteBatch);
			Vector2 boxPos = new(dim.X, dim.Y + dim.Height * (scrollBar.ViewPosition / scrollBarMaxViewSize));
			float boxWidth = 20f * Main.UIScale;
			float boxHeight = dim.Height * (scrollBarViewSize / scrollBarMaxViewSize);
			if (scrollBarFocus)
			{
				if (curMouse.LeftButton == ButtonState.Released)
				{
					scrollBarFocus = false;
				}
				else
				{
					int difference = curMouse.Y - scrollBarFocusMouseStart;
					scrollBar.ViewPosition = scrollBarFocusPositionStart + difference / boxHeight;
				}
			}
			else if (MouseClicked)
			{
				if (curMouse.X > boxPos.X && curMouse.X < boxPos.X + boxWidth && curMouse.Y > boxPos.Y - 3f && curMouse.Y < boxPos.Y + boxHeight + 4f)
				{
					scrollBarFocus = true;
					scrollBarFocusMouseStart = curMouse.Y;
					scrollBarFocusPositionStart = scrollBar.ViewPosition;
				}
			}

			if (!scrollBarFocus)
			{
				int difference = oldMouse.ScrollWheelValue / 250 - curMouse.ScrollWheelValue / 250;
				scrollBar.ViewPosition += difference;
			}
		}

		public static TEStorageHeart GetHeart() => StoragePlayer.LocalPlayer.GetStorageHeart();

		public static void RefreshItems()
		{
			if (StoragePlayer.IsStorageCrafting())
			{
				CraftingGUI.RefreshItems();
				return;
			}

			if (StoragePlayer.IsStorageEnvironment())
				return;

			items.Clear();
			didMatCheck.Clear();
			TEStorageHeart heart = GetHeart();
			if (heart == null)
				return;

			NetHelper.Report(true, "Refreshing storage items");

			InitLangStuff();
			InitSortButtons();
			InitFilterButtons();
			SortMode sortMode = (SortMode)sortButtons.Choice;

			FilterMode filterMode = ItemFilter.GetFilter(filterButtons.Choice);
			int modFilterIndex = modSearchBox.ModIndex;

			void DoFiltering()
			{
				IEnumerable<Item> itemsLocal;
				if (filterMode == FilterMode.Recent)
				{
					Dictionary<int, Item> stored = heart.GetStoredItems().GroupBy(x => x.type).ToDictionary(x => x.Key, x => x.First());

					IEnumerable<Item> toFilter = heart.UniqueItemsPutHistory.Reverse().Where(x => stored.ContainsKey(x.type)).Select(x => stored[x.type]);
					itemsLocal = ItemSorter.SortAndFilter(toFilter, sortMode == SortMode.Default ? SortMode.AsIs : sortMode, FilterMode.All, modFilterIndex, searchBar.Text, 100);
				}
				else
				{
					itemsLocal = ItemSorter.SortAndFilter(heart.GetStoredItems(), sortMode, filterMode, modFilterIndex, searchBar.Text).OrderBy(x => x.favorited ? 0 : 1);
				}

				items.AddRange(itemsLocal.Where(x => !favoritedOnlyButton.Value || x.favorited));

				NetHelper.Report(false, "Filtering applied.  Item count: " + items.Count);
			}

			DoFiltering();

			// now if nothing found we disable filters one by one
			if (searchBar.Text.Trim().Length > 0)
			{
				if (items.Count == 0 && filterMode != FilterMode.All)
				{
					// search all categories
					filterMode = FilterMode.All;

					NetHelper.Report(false, "No items displayed even though items exist in storage.  Defaulting to \"All\" filter mode");

					DoFiltering();
				}

				if (items.Count == 0 && modFilterIndex != ModSearchBox.ModIndexAll)
				{
					// search all mods
					modFilterIndex = ModSearchBox.ModIndexAll;

					NetHelper.Report(false, $"No items displayed even though items exist in storage.  Defaulting to \"{Language.GetTextValue("Mods.MagicStorage.FilterAllMods")}\" mod filter");

					DoFiltering();
				}
			}

			for (int k = 0; k < items.Count; k++)
				didMatCheck.Add(false);
		}

		private static void UpdateDepositButton()
		{
			Rectangle dim = InterfaceHelper.GetFullRectangle(depositButton);
			if (curMouse.X > dim.X && curMouse.X < dim.X + dim.Width && curMouse.Y > dim.Y && curMouse.Y < dim.Y + dim.Height)
			{
				depositButton.BackgroundColor = new Color(73, 94, 171);
				if (MouseClicked)
				{
					bool ctrlDown = Main.keyState.IsKeyDown(Keys.LeftControl) || Main.keyState.IsKeyDown(Keys.RightControl);
					if (TryDepositAll(ctrlDown == MagicStorageConfig.QuickStackDepositMode))
					{
						RefreshItems();
						SoundEngine.PlaySound(SoundID.Grab);
					}
				}
				else if (CraftingGUI.RightMouseClicked)
				{
					if (TryRestock())
					{
						RefreshItems();
						SoundEngine.PlaySound(SoundID.Grab);
					}
				}
			}
			else
			{
				depositButton.BackgroundColor = new Color(63, 82, 151) * 0.7f;
			}
		}

		private static void DrawDepositButton()
		{
			Rectangle dim = InterfaceHelper.GetFullRectangle(depositButton);
			if (curMouse.X > dim.X && curMouse.X < dim.X + dim.Width && curMouse.Y > dim.Y && curMouse.Y < dim.Y + dim.Height)
			{
				string alt = MagicStorageConfig.QuickStackDepositMode ? "Alt" : "";
				Main.instance.MouseText(Language.GetText($"Mods.MagicStorage.DepositTooltip{alt}").Value);
			}
		}

		private static void ResetSlotFocus()
		{
			slotFocus = -1;
			rightClickTimer = 0;
			maxRightClickTimer = startMaxRightClickTimer;
		}

		private static void HoverItemSlot(int slot, ref int hoverSlot)
		{
			Player player = Main.LocalPlayer;
			int visualSlot = slot;
			slot += numColumns * (int)Math.Round(scrollBar.ViewPosition);

			if (MouseClicked)
			{
				bool changed = false;
				if (!Main.mouseItem.IsAir && player.itemAnimation == 0 && player.itemTime == 0)
				{
					if (TryDeposit(Main.mouseItem))
						changed = true;
				}
				else if (Main.mouseItem.IsAir && slot < items.Count && !items[slot].IsAir)
				{
					if (Main.keyState.IsKeyDown(Keys.LeftAlt))
					{
						if (Main.netMode == NetmodeID.SinglePlayer)
							items[slot].favorited = !items[slot].favorited;
						else
							Main.NewTextMultiline(
								"Toggling item as favorite is not implemented in multiplayer but you can withdraw this item, toggle it in inventory and deposit again",
								c: Color.White);
						// there is no item instance id and there is no concept of slot # in heart so we can't send this in operation
						// a workaropund would be to withdraw and deposit it back with changed favorite flag
						// but it still might look ugly for the player that initiates operation
					}
					else
					{
						Item toWithdraw = items[slot].Clone();
						if (toWithdraw.stack > toWithdraw.maxStack)
							toWithdraw.stack = toWithdraw.maxStack;
						Main.mouseItem = DoWithdraw(toWithdraw, ItemSlot.ShiftInUse);
						if (ItemSlot.ShiftInUse)
							Main.mouseItem = player.GetItem(Main.myPlayer, Main.mouseItem, GetItemSettings.InventoryEntityToPlayerInventorySettings);
						changed = true;
					}
				}

				RefreshItems();
				if (changed)
				{
					RefreshItems();
					SoundEngine.PlaySound(SoundID.Grab);
				}
			}

			if (RightMouseClicked &&
				slot < items.Count &&
				(Main.mouseItem.IsAir || ItemData.Matches(Main.mouseItem, items[slot]) && Main.mouseItem.stack < Main.mouseItem.maxStack))
				slotFocus = slot;

			if (slot < items.Count && !items[slot].IsAir)
			{
				hoverSlot = visualSlot;
				items[slot].newAndShiny = false;
			}

			if (slotFocus >= 0)
				SlotFocusLogic();
		}

		private static void SlotFocusLogic()
		{
			if (slotFocus >= items.Count ||
				!Main.mouseItem.IsAir && (!ItemData.Matches(Main.mouseItem, items[slotFocus]) || Main.mouseItem.stack >= Main.mouseItem.maxStack))
			{
				ResetSlotFocus();
			}
			else
			{
				if (rightClickTimer <= 0)
				{
					rightClickTimer = maxRightClickTimer;
					maxRightClickTimer = maxRightClickTimer * 3 / 4;
					if (maxRightClickTimer <= 0)
						maxRightClickTimer = 1;
					Item toWithdraw = items[slotFocus].Clone();
					toWithdraw.stack = 1;
					Item result = DoWithdraw(toWithdraw);
					if (Main.mouseItem.IsAir)
						Main.mouseItem = result;
					else
						Main.mouseItem.stack += result.stack;

					RefreshItems();
					SoundEngine.PlaySound(SoundID.MenuTick);
				}

				rightClickTimer--;
			}
		}

		/// <summary>
		/// Simulates a deposit attempt into a storage center (Storage Heart, Storage Access, etc.).
		/// </summary>
		/// <param name="center">The tile entity used to attempt to retrieve a Storage Heart</param>
		/// <param name="item">The item to deposit</param>
		/// <returns>Whether the deposit was successful</returns>
		public static bool TryDespoit(TEStorageCenter center, Item item) {
			if (center is null)
				return false;

			StoragePlayer.StorageHeartAccessWrapper wrapper = new(center);

			if (wrapper.Valid) {
				int oldStack = item.stack;
				TEStorageHeart heart = wrapper.Heart;
				heart.TryDeposit(item);
				return oldStack != item.stack;
			}
			
			return false;
		}

		/// <summary>
		/// Simulates a deposit attempt into the currently assigned Storage Heart.
		/// </summary>
		/// <param name="item">The item to deposit</param>
		/// <returns>Whether the deposit was successful</returns>
		public static bool TryDeposit(Item item)
		{
			int oldStack = item.stack;
			TEStorageHeart heart = GetHeart();
			heart.TryDeposit(item);
			return oldStack != item.stack;
		}

		/// <summary>
		/// Simulates a deposit attempt into a storage center (Storage Heart, Storage Access, etc.).
		/// </summary>
		/// <param name="center">The tile entity used to attempt to retrieve a Storage Heart</param>
		/// <param name="items">The items to deposit</param>
		/// <returns>Whether the deposit was successful</returns>
		public static bool TryDeposit(TEStorageCenter center, List<Item> items)
		{
			if (center is null)
				return false;

			StoragePlayer.StorageHeartAccessWrapper wrapper = new(center);

			if (wrapper.Valid) {
				TEStorageHeart heart = wrapper.Heart;
				return heart.TryDeposit(items);
			}

			return false;
		}

		/// <summary>
		/// Simulates a deposit attempt into the currently assigned Storage Heart.
		/// </summary>
		/// <param name="items">The items to deposit</param>
		/// <returns>Whether the deposit was successful</returns>
		public static bool TryDeposit(List<Item> items)
		{
			TEStorageHeart heart = GetHeart();
			return heart.TryDeposit(items);
		}

		private static bool TryDepositAll(bool quickStack)
		{
			Player player = Main.LocalPlayer;
			TEStorageHeart heart = GetHeart();

			bool filter(Item item) => !item.IsAir && !item.favorited && (!quickStack || heart.HasItem(item, true));
			var items = new List<Item>();

			for (int k = 10; k < 50; k++)
			{
				Item item = player.inventory[k];
				if (filter(item))
					items.Add(item);
			}

			return heart.TryDeposit(items);
		}

		private static bool TryRestock()
		{
			Player player = Main.LocalPlayer;
			GetHeart();
			bool changed = false;

			foreach (Item item in player.inventory)
				if (item is not null && !item.IsAir && item.stack < item.maxStack)
				{
					Item toWithdraw = item.Clone();
					toWithdraw.stack = item.maxStack - item.stack;
					toWithdraw = DoWithdraw(toWithdraw, true, true);
					if (!toWithdraw.IsAir)
					{
						item.stack += toWithdraw.stack;
						toWithdraw.TurnToAir();
						changed = true;
					}
				}

			return changed;
		}

		/// <summary>
		/// Attempts to withdraw an item from a storage center (Storage Heart, Storage Access, etc.).
		/// </summary>
		/// <param name="center">The tile entity used to attempt to retrieve a Storage Heart</param>
		/// <param name="item">The item to withdraw</param>
		/// <param name="toInventory">
		/// Whether the item goes into the player inventory.
		/// This parameter is only used if <see cref="Main.netMode"/> is <see cref="NetmodeID.MultiplayerClient"/>
		/// </param>
		/// <param name="keepOneIfFavorite">Whether at least one item should remain in the storage if it's favourited</param>
		/// <returns>A valid item instance if the withdrawal was succesful, an air item otherwise.</returns>
		public static Item DoWithdraw(TEStorageCenter center, Item item, bool toInventory = false, bool keepOneIfFavorite = false)
		{
			if (center is null)
				return new Item();

			StoragePlayer.StorageHeartAccessWrapper wrapper = new(center);

			if (wrapper.Valid) {
				TEStorageHeart heart = wrapper.Heart;
				return heart.TryWithdraw(item, keepOneIfFavorite, toInventory);
			}

			return new Item();
		}

		/// <summary>
		/// Attempts to withdraw an item from the currently assigned Storage Heart.
		/// </summary>
		/// <param name="item">The item to withdraw</param>
		/// <param name="toInventory">
		/// Whether the item goes into the player inventory.
		/// This parameter is only used if <see cref="Main.netMode"/> is <see cref="NetmodeID.MultiplayerClient"/>
		/// </param>
		/// <param name="keepOneIfFavorite">Whether at least one item should remain in the storage if it's favourited</param>
		/// <returns>A valid item instance if the withdrawal was succesful, an air item otherwise.</returns>
		public static Item DoWithdraw(Item item, bool toInventory = false, bool keepOneIfFavorite = false)
		{
			TEStorageHeart heart = GetHeart();
			return heart.TryWithdraw(item, keepOneIfFavorite, toInventory);
		}
	}
}
