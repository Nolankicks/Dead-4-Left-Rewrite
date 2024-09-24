using System;
using Sandbox;
using Sandbox.Events;

public record OnItemEquipped() : IGameEvent;

public sealed class Inventory : Component
{
	[Property] public List<WeaponData> StartingWeapons { get; set; } = new();

	[Property, Sync] public List<GameObject> Items { get; set; } = new();
	[Property, Sync] public List<WeaponData> ItemsData { get; set; } = new();

	[Property, Sync] public int Index { get; set; } = 0;

	[Property, Sync] public GameObject CurrentItem { get; set; }
	[Property, Sync] public WeaponData CurrentWeaponData { get; set; }

	protected override void OnStart()
	{
		if ( IsProxy )
			return;

		foreach ( var weapon in StartingWeapons )
		{
			AddItem( weapon );
		}

		ChangeItem( Index, Items );
	}

	[Button]
	public void SwapItemsButton()
	{
		SwapItems( 2, 1 );

		var hud = Scene.GetAll<HUD>()?.FirstOrDefault();

		if ( hud.IsValid() )
			hud.StateHasChanged();
	}

	public void SwapItems( int index1, int index2 )
	{
		if ( Items?.Count() == 0 || Items is null )
			return;

		if ( index1 < 0 || index1 >= Items.Count() || index2 < 0 || index2 >= Items.Count() )
		{
			Log.Warning( "Invalid index" );
			return;
		}

		var temp = Items[index1];
		Items[index1] = Items[index2];
		Items[index2] = temp;
		
		var tempData = ItemsData[index1];
		ItemsData[index1] = ItemsData[index2];
		ItemsData[index2] = tempData;

		if ( Index == index1 )
			Index = index2;
		else if ( Index == index2 )
			Index = index1;
	}

	[Description("Swaps the items in the inventory, please null check the GameObjects before calling this method.")]
	public void SwapItems( GameObject gb1, GameObject gb2 )
	{
		if ( !gb1.IsValid() || !gb2.IsValid() )
			return;

		var index1 = Items.IndexOf( gb1 );
		var index2 = Items.IndexOf( gb2 );

		if ( index1 == -1 || index2 == -1 )
			return;

		SwapItems( index1, index2  );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || Items?.Count() == 0 )
			return;

		if ( Input.MouseWheel.y != 0 )
		{
			Index = (Index - Math.Sign( Input.MouseWheel.y )) % Items.Count();
			ChangeItem( Index, Items );
		}

		if ( Index < 0 )
		{
			Index = Items.Count() - 1;
			ChangeItem( Index, Items );
		}

		KeyboardInputs();
	}

	public void AddItem( WeaponData item )
	{
		if ( IsProxy )
			return;

		var clone = item.WeaponPrefab.Clone();

		var local = PlayerController.Local;

		if ( !clone.IsValid() || !local.IsValid() || (!local?.Eye.IsValid() ?? false) )
			return;

		clone.Parent = local.Eye;

		clone.Enabled = false;

		clone.NetworkSpawn( false, Network.Owner );

		Items.Add( clone );
		ItemsData.Add( item );
	}

	public void ChangeItem( int index, List<GameObject> items )
	{
		if ( index < 0 || index >= items?.Count() || items is null )
			return;

		Index = index;

		if ( CurrentItem.IsValid() )
			CurrentItem.Enabled = false;

		var nextItem = items?[index];

		if ( !nextItem.IsValid() )
			return;

		CurrentItem = nextItem;

		CurrentWeaponData = ItemsData[index];

		if ( !CurrentItem.IsValid() )
			return;

		CurrentItem.Enabled = true;

		CurrentItem.Dispatch( new OnItemEquipped() );
	}

	public void KeyboardInputs()
	{
		if ( Input.Pressed( "slot1" ) )
		{
			ChangeItem( 0, Items );
		}

		if ( Input.Pressed( "slot2" ) )
		{
			ChangeItem( 1, Items );
		}

		if ( Input.Pressed( "slot3" ) )
		{
			ChangeItem( 2, Items );
		}

		if ( Input.Pressed( "slot4" ) )
		{
			ChangeItem( 3, Items );
		}

		if ( Input.Pressed( "slot5" ) )
		{
			ChangeItem( 4, Items );
		}

		if ( Input.Pressed( "slot6" ) )
		{
			ChangeItem( 5, Items );
		}

		if ( Input.Pressed( "slot7" ) )
		{
			ChangeItem( 6, Items );
		}

		if ( Input.Pressed( "slot8" ) )
		{
			ChangeItem( 7, Items );
		}

		if ( Input.Pressed( "slot9" ) )
		{
			ChangeItem( 8, Items );
		}
	}
}
