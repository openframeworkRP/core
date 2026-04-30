using Sandbox;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFramework;

public sealed class ViewModelDresser : Component
{
	[Property] public SkinnedModelRenderer ViewmodelArms { get; set; }

	public async void Sync( PlayerPawn owner )
	{
		if ( !ViewmodelArms.IsValid() || owner == null ) return;

		await Task.Frame();
		await Task.Frame();

		// Apres les await, le ViewmodelArms peut avoir ete detruit (swap d'arme,
		// death/respawn, changement de scene). Re-verifier avant toute operation
		// pour eviter "Can't parent to a GameObject in a different Scene".
		if ( !ViewmodelArms.IsValid() || !ViewmodelArms.GameObject.IsValid() || owner == null || !owner.IsValid() ) return;

		var dresser = owner.Components.Get<Dresser>( FindMode.EverythingInSelfAndChildren );
		if ( dresser == null ) return;

		var bodySource = dresser.BodyTarget;
		if ( !bodySource.IsValid() ) return;

		// S'assurer que le bodySource est dans la meme scene que le viewmodel
		if ( bodySource.GameObject.Scene != ViewmodelArms.GameObject.Scene ) return;

		// 1. Nettoyage des anciens v�tements Viewmodel
		var toDestroy = ViewmodelArms.GameObject.Children
			.Where( x => x.Tags.Has( "viewmodel_clothing" ) )
			.ToList();
		foreach ( var child in toDestroy ) child.Destroy();

		// --- LOGIQUE DE D�TECTION ---
		bool hasGloves = false;
		//bool hasLongSleeves = false;

		// 2. Scan des habits du joueur
		var clothingRenders = bodySource.GameObject.Components.GetAll<SkinnedModelRenderer>( FindMode.EnabledInSelfAndChildren );

		foreach ( var source in clothingRenders )
		{
			if ( source == bodySource || source.Model == null ) continue;

			var path = source.Model.ResourcePath.ToLower();

			// On cr�e le v�tement en FPS
			if ( path.Contains( "tops" ) || path.Contains( "vest" ) || path.Contains( "jacket" ) ||
				 path.Contains( "watch" ) || path.Contains( "gloves" ) || path.Contains( "shirt" ) )
			{
				CreateViewModelClothing( source );

				// On check si c'est des gants
				if ( path.Contains( "gloves" ) )
				{
					hasGloves = true;
				}

				// On check si c'est un v�tement � manches longues (Veste, Gilet, etc.)
				if ( path.Contains( "jacket" ) || path.Contains( "vest" ) || (path.Contains( "tops" ) && !path.Contains( "tshirt" )) )
				{
					//hasLongSleeves = true;
				}
			}
		}

		// 3. APPLICATION FINALE DES BODYGROUPS
		// R�gle : Si PAS de gants, on montre les mains (index 0)
		// Si gants OU manches longues vraiment couvrantes, on cache (index 1)

		if ( hasGloves )
		{
			ViewmodelArms.SetBodyGroup( "arms", 1 ); // Cache les mains car les gants les remplacent
		}
		else
		{
			// PAS de gants : On force l'affichage des mains
			ViewmodelArms.SetBodyGroup( "arms", 0 );
		}

		// Note : Si tu as des manches longues mais pas de gants, 
		// le v�tement 3D de la manche couvrira le bras, mais les mains resteront visibles.
	}

	private void CreateViewModelClothing( SkinnedModelRenderer source )
	{
		// Le ViewmodelArms peut etre detruit entre deux sources si le joueur swap
		// d'arme pendant l'execution de Sync (async). Abort proprement.
		if ( !ViewmodelArms.IsValid() || !ViewmodelArms.GameObject.IsValid() ) return;

		// On cree l'objet DIRECTEMENT dans la scene du viewmodel (peut differer
		// de Game.ActiveScene a l'equipement d'arme) sinon set_Parent throw
		// "Can't parent to a GameObject in a different Scene".
		var clothesObj = ViewmodelArms.GameObject.Scene.CreateObject();
		clothesObj.Name = $"VM_{source.GameObject.Name}";
		clothesObj.Parent = ViewmodelArms.GameObject;
		clothesObj.Tags.Add( "viewmodel_clothing" );

		var vmRenderer = clothesObj.Components.Create<SkinnedModelRenderer>();
		vmRenderer.Model = source.Model;
		vmRenderer.BoneMergeTarget = ViewmodelArms;

		vmRenderer.Tint = source.Tint; // Garde ton orange !
		vmRenderer.MaterialGroup = source.MaterialGroup;
		vmRenderer.BodyGroups = source.BodyGroups;
		vmRenderer.RenderType = ModelRenderer.ShadowRenderType.Off;
		vmRenderer.RenderOptions.Overlay = true;
		vmRenderer.RenderOptions.Game = false;
	}
}
