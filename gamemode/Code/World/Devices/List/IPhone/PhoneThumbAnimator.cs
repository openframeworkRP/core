public class PhoneThumbAnimator : Component
{
	[Property] public GameObject ThumbBone { get; set; }
	[Property] public PanelComponent PhonePanel { get; set; } // Le WorldPanel du téléphone

	private Vector3 _basePos;

	protected override void OnStart()
	{
		if ( ThumbBone != null )
			_basePos = ThumbBone.LocalPosition;
	}

	protected override void OnUpdate()
	{
		if ( ThumbBone == null || PhonePanel == null || PhonePanel.Panel == null ) return;

		// Ray depuis la caméra vers la souris
		var ray = Scene.Camera.ScreenPixelToRay( Mouse.Position );

		// Intersection avec le WorldPanel
		if ( !PhonePanel.Panel.RayToLocalPosition( ray, out var localPos, out _ ) )
			return;

		// localPos est en pixels dans le panel (ex: 0,0 = coin haut gauche, 390,844 = coin bas droit)
		// On normalise entre -0.5 et 0.5
		var panelSize = PhonePanel.Panel.Box.Rect.Size;
		var normalized = new Vector2(
			(localPos.x / panelSize.x) - 0.5f,
			(localPos.y / panelSize.y) - 0.5f
		);

		// On mappe les coordonnées normalisées sur les axes locaux du bone
		var target = _basePos + new Vector3(
			normalized.x * 300f,   // amplitude horizontale
			0f,
			-normalized.y * 600f   // amplitude verticale (Z local = haut/bas)
		);

		// Lerp souple
		ThumbBone.LocalPosition = Vector3.Lerp(
			ThumbBone.LocalPosition,
			target,
			Time.Delta * 20f
		);
	}
}
