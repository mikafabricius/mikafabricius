using Godot;
using System;

public partial class topBar : PanelContainer
{
	MenuButton viewButton;
	PanelContainer sideBar;

	public override void _Ready()
	{
		viewButton = (MenuButton)GetNode("HBoxContainer/viewButton");
		sideBar = (PanelContainer)GetNode("../canvas/sideBar");
		GD.Print("Hello from topBar.cs \n");
		viewButton.GetPopup().Connect("id_pressed", new Callable(this, MethodName.ViewButton));
	}
	
	private void ViewButton(long id)
	{
		switch (id) 
		{
			case 0:
			{
				sideBar.Visible = !sideBar.Visible;
				break;
			}
			default:
				break;
			
		}
	}
}
