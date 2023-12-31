using Godot;

public class Gun : Spatial
{
	[Export]
	public PackedScene Rocket;
	[Export]
	public PackedScene BulletSplashScene;
	public GunStats GunStats = new GunStats();
	public GunMods GunMods;

	private float _muzzleTimer;
	private Spatial _muzzleFlash;
	private float _fireCooldown;
	private Player _player;
	private AnimationPlayer _animationPlayer;
	private Rocket[] _rockets = new Rocket[3];
	private float _rocketReloadTimer;

	public override void _Ready()
	{
		_muzzleFlash = GetNode<Spatial>("Gun_v3/Armature/Skeleton/BoneAttachment9/MuzzleFlash");
		_muzzleFlash.Hide();

		var bullet = GetNode<Spatial>("Bullet") as Bullet;
		Bullet.InitializePool(bullet);
		bullet.Hide();

		BulletSplash.InitializePool(this, BulletSplashScene);

		var bulletHole = GetNode<Spatial>("BulletHole") as BulletHole;
		BulletHole.InitializePool(bulletHole);
		bulletHole.Hide();

		_player = GetTree().Root.FindNode("Player", true, false) as Player;
		_animationPlayer = GetNode<AnimationPlayer>("AnimationPlayerEvents");

		GunMods = new GunMods(this);

		var gunModel = GetNode<Spatial>("Gun_v3");
		var meshInstances = NodeHelper.GetChildren<MeshInstance>(gunModel);
		foreach (var mesh in meshInstances)
		{
			mesh.Layers = 2;
		}

		ReloadRockets();
	}

	public override void _Process(float delta)
	{
		if (_muzzleTimer > 0)
		{
			_muzzleTimer -= delta;
			if (_muzzleTimer < 0)
				_muzzleFlash.Hide();
		}

		if (_fireCooldown > 0)
			_fireCooldown -= delta;

		if (_rocketReloadTimer > 0)
		{
			_rocketReloadTimer -= delta;
			if (_rocketReloadTimer <= 0)
			{
				ReloadRockets();
			}
		}
	}

	public bool CanFireBullet()
	{
		return _fireCooldown <= 0;
	}

	public bool CanFireRocket()
	{
		return _rocketReloadTimer <= 0;
	}

	public void PullTrigger()
	{
		if (CanFireBullet())
		{
			_animationPlayer.Stop();
			_animationPlayer.Play(GunStats.FireAnimation);
		}
	}

	public void FireBullet()
	{
		for (int i = 0; i < GunStats.Projectiles; i++)
		{
			var playerHead = _player.GetNode<Spatial>("Head");
			var origin = playerHead.GlobalTransform.origin;
			var direction = -playerHead.GlobalTransform.basis.z.Normalized();

			var maxSpreadAngle = GunStats.Spread / 20;
			float yaw = (float)GD.RandRange(-maxSpreadAngle, maxSpreadAngle);
			direction = direction.Rotated(Vector3.Up, Mathf.Deg2Rad(yaw));
			float pitch = (float)GD.RandRange(-maxSpreadAngle, maxSpreadAngle);
			direction = direction.Rotated(Vector3.Right, Mathf.Deg2Rad(pitch));

			_fireCooldown = GunStats.FireCooldown;
			if (_fireCooldown <= 0.01f)
				_fireCooldown = 0.01f;

			_muzzleFlash.Show();
			_muzzleTimer = 0.1f;

			var clonedBullet = default(Bullet);
			if (i < 5)
			{
				clonedBullet = Bullet.GetBullet();
				clonedBullet.GlobalTranslation = origin;
				clonedBullet.LookAt(origin + direction, Vector3.Up);
				clonedBullet.Translate(Vector3.Right * 0.5f + Vector3.Down * 0.2f);
				clonedBullet.Show();
			}

			var hit = BulletRayCast(origin, direction);
			if (hit.Count > 0)
			{
				var hitPoint = (Vector3)hit["position"];

				var clonedBulletSplash = BulletSplash.GetBulletSplash();
				clonedBulletSplash.GlobalTranslation = hitPoint;
				clonedBulletSplash.Emitting = true;

				var hitEntity = hit["collider"] as Spatial;
				var hitNormal = (Vector3)hit["normal"];

				//				var clonedBulletHole = BulletHole.GetBulletHole();
				//				NodeHelper.ReparentNode(clonedBulletHole, hitEntity);
				//				clonedBulletHole.GlobalTransform = new Transform(new Quat(), hitPoint + hitNormal * .01f).LookingAt(hitPoint + hitNormal, (Vector3.Up + Vector3.Forward).Normalized());
				//				clonedBulletHole.Show();

				if (hitEntity is IShootable shootable)
				{
					if (clonedBullet != null)
						clonedBullet.MaxDistance = (hitPoint - origin).Length();
					shootable.Shot(hitPoint);
				}
			}
		}

		var player = GetTree().Root.FindNode("Player", true, false) as Player;
		if (player != null)
		{
			var gun = player.GetNode<Gun>("Head/GunHolder/Gun");
			if (gun != null)
			{
				var head = player.GetNode<Spatial>("Head");
				head.RotateX(Mathf.Deg2Rad(gun.GunStats.Recoil > 0 ? gun.GunStats.Recoil : 0));
			}
		}
	}

	public Godot.Collections.Dictionary BulletRayCast(Vector3 origin, Vector3 direction)
	{
		Vector3 rayOrigin = origin;
		Vector3 rayEnd = direction * 1000 + rayOrigin;

		PhysicsDirectSpaceState spaceState = GetWorld().DirectSpaceState;
		uint collisionMask = 1; // 1000
		Godot.Collections.Dictionary hit = spaceState.IntersectRay(rayOrigin, rayEnd, null, collisionMask);

		return hit;
	}

	public void ReloadRockets()
	{
		for (int i = 0; i < 3; i++)
		{
			var rocketHolder = GetNode<Spatial>($"Gun_v3/Armature/Skeleton/BoneAttachment20/Rocket Launcher/RocketHolder{i + 1}");
			var rocket = Rocket.Instance() as Rocket;
			rocketHolder.AddChild(rocket);
			rocket.Translation = Vector3.Zero;
			rocket.Rotation = new Vector3(0, Mathf.Deg2Rad(180), 0);
			rocket.Scale = Vector3.One;
			_rockets[i] = rocket;
		}
	}

	public void FireRockets()
	{
		for (int i = 0; i < 3; i++)
		{
			var rocket = _rockets[i];
			if (rocket != null)
			{
				rocket.Fire();
				_rockets[i] = null;
			}
		}

		_rocketReloadTimer = 3;
	}
}
