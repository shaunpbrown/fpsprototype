using System.Collections.Generic;
using Godot;

public class Printer : Spatial, IInteractable
{
    private float _openCloseSpeed = 4f;
    private Spatial _gunHolder;
    private Spatial _cameraHolder;
    private Spatial _topBox;
    private bool _isOpen;
    private bool _isClosed;
    private Player _player;
    private bool _isBeingUsed;
    private bool _isGunInHolder;
    private bool _isPlayerExiting;
    private Vector2 _lastMousePos = new Vector2();
    private CanvasLayer _printerUI;
    private List<UpgradeCard> _upgradeCards = new List<UpgradeCard>();
    private Spatial _currentHoloMod;

    public override void _Ready()
    {
        _player = GetTree().Root.FindNode("Player", true, false) as Player;

        _gunHolder = GetNode<Spatial>("GunHolder");
        _cameraHolder = GetNode<Spatial>("CameraHolder");
        _topBox = GetNode<Spatial>("Top");
        _isOpen = false;
        _isClosed = true;
        _topBox.Translation = new Vector3(0, 1.5f, 0);

        _printerUI = GetNode<CanvasLayer>("PrinterUI");
        _printerUI.Visible = false;
        _upgradeCards.Add(GetNode<UpgradeCard>("PrinterUI/Panel/UpgradeCard1"));
        _upgradeCards.Add(GetNode<UpgradeCard>("PrinterUI/Panel/UpgradeCard2"));
        _upgradeCards.Add(GetNode<UpgradeCard>("PrinterUI/Panel/UpgradeCard3"));
        foreach (var card in _upgradeCards)
        {
            card.SelectedAction = UpgradeCardSelected;
        }

        var confirmButton = GetNode<Button>("PrinterUI/Panel/ConfirmButton");
        confirmButton.Connect("pressed", this, nameof(UpgradeCardConfirmed));

        //temp
        _upgradeCards[0].ModName = "Accuracy";
        _upgradeCards[1].ModName = "Rocket Launcher";
    }

    public override void _Process(float delta)
    {
        if (_isBeingUsed)
        {

            if (_isPlayerExiting)
            {
                GiveGunBackToPlayer(delta);
            }
            else if (!_isGunInHolder)
            {
                PutGunInHolder(delta);
            }
            else
            {

                if (Input.IsMouseButtonPressed((int)ButtonList.Left))
                {
                    var gun = _gunHolder.GetNode<Gun>("Gun");

                    Vector2 currentMousePos = GetViewport().GetMousePosition();
                    Vector2 mouseDelta = currentMousePos - _lastMousePos;
                    _lastMousePos = currentMousePos; // Update the last known mouse position
                    Vector3 rotationDelta = new Vector3(-mouseDelta.y, mouseDelta.x, 0) * delta;
                    Vector3 rotationRadians = rotationDelta * Mathf.Pi / 180;

                    var rotateSpeed = 600f;
                    gun.RotateZ(rotationRadians.x * delta * rotateSpeed * 2);
                    gun.RotateY(rotationRadians.y * delta * rotateSpeed);
                }
                _lastMousePos = GetViewport().GetMousePosition();
            }
        }
        else
        {
            OpenAndCloseUpdate(delta);
        }
    }

    public string GetInteractText()
    {
        return "Press E to use printer";
    }

    public void Interact(Player player)
    {
        var gun = player.GetNode<Gun>("Head/GunHolder/Gun");
        NodeHelper.ReparentNode(gun, _gunHolder);

        var camera = player.GetNode<Camera>("Head/Camera");
        NodeHelper.ReparentNode(camera, _cameraHolder);

        _isBeingUsed = true;
        _player.IsUsingPrinter = true;
        _player.SetInteractText("");
    }

    public void PutGunInHolder(float delta)
    {
        var gun = _gunHolder.GetNode<Gun>("Gun");
        var camera = _cameraHolder.GetNode<Camera>("Camera");
        MoveTowardsOrigin(gun, delta * 3f);
        if (gun.Translation.Length() < .01f)
        {
            gun.Translation = Vector3.Zero;
            _isGunInHolder = true;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            camera.Translation = Vector3.Zero;
            _printerUI.Visible = true;
        }

        MoveTowardsOrigin(camera, delta * 3f);
        if (camera.Translation.Length() < .01f)
        {
            camera.Translation = Vector3.Zero;
        }
    }

    public void GiveGunBackToPlayer(float delta)
    {
        var camera = _player.GetNode<Camera>("Head/Camera");
        var gun = _player.GetNode<Gun>("Head/GunHolder/Gun");
        MoveTowardsOrigin(gun, delta * 6f);
        if (gun.Translation.Length() < .01f)
        {
            gun.Translation = Vector3.Zero;
            Input.MouseMode = Input.MouseModeEnum.Captured;
            camera.Translation = Vector3.Zero;
            _isBeingUsed = false;
            _isGunInHolder = false;
            _isPlayerExiting = false;
            _player.IsUsingPrinter = false;
        }

        MoveTowardsOrigin(camera, delta * 6f);
        if (camera.Translation.Length() < .01f)
        {
            camera.Translation = Vector3.Zero;
        }
    }

    public void MoveTowardsOrigin(Spatial target, float weight)
    {
        Vector3 newPosition = target.GlobalTranslation.LinearInterpolate(target.GetParent<Spatial>().ToGlobal(Vector3.Zero), weight);

        Quat currentRot = new Quat(target.GlobalTransform.basis).Normalized();
        Quat desiredRot = new Quat(target.GetParent<Spatial>().GlobalTransform.basis).Normalized();

        Quat newRotation = currentRot.Slerp(desiredRot, weight);

        target.GlobalTransform = new Transform(newRotation, newPosition);
    }

    public void OpenAndCloseUpdate(float delta)
    {
        var playerInteractRayHit = _player.InteractRayCast();
        var node = playerInteractRayHit.Count == 0 ? null : playerInteractRayHit["collider"] as Node;
        if (node == this)
        {
            _isClosed = false;
            if (!_isOpen)
            {
                _topBox.Translate(Vector3.Up * _openCloseSpeed * delta);
                if (_topBox.Translation.y >= 2.5f)
                {
                    _topBox.Translation = new Vector3(0, 2.5f, 0);
                    _isOpen = true;
                }
            }
        }
        else
        {
            _isOpen = false;
            if (!_isClosed)
            {
                _topBox.Translate(Vector3.Down * _openCloseSpeed * delta);
                if (_topBox.Translation.y <= 1.5f)
                {
                    _topBox.Translation = new Vector3(0, 1.5f, 0);
                    _isClosed = true;
                }
            }
        }
    }

    public void UpgradeCardSelected(UpgradeCard selectedCard)
    {
        var gun = _gunHolder.GetNode<Gun>("Gun");
        if (gun == null)
            return;

        if (_currentHoloMod != null)
        {
            _currentHoloMod.Visible = false;
            _currentHoloMod = null;
        }

        foreach (var card in _upgradeCards)
        {
            if (card != selectedCard)
                card.Pressed = false;
        }

        if (string.IsNullOrEmpty(selectedCard.ModName))
            return;

        var holoMod = NodeHelper.GetChildNode(selectedCard.ModName + "HOLO", gun) as Spatial;
        holoMod.Visible = true;
        _currentHoloMod = holoMod;
    }

    public void UpgradeCardConfirmed()
    {
        var gun = _gunHolder.GetNode<Gun>("Gun");
        if (_currentHoloMod != null)
        {
            var mod = NodeHelper.GetChildNode(_currentHoloMod.Name.Replace("HOLO", ""), gun) as Spatial;
            mod.Visible = true;
            _currentHoloMod.Visible = false;
            _currentHoloMod = null;
        }

        _isPlayerExiting = true;
        _isGunInHolder = false;

        NodeHelper.ReparentNode(gun, _player.GetNode<Spatial>("Head/GunHolder"));

        var camera = _cameraHolder.GetNode<Camera>("Camera");
        NodeHelper.ReparentNode(camera, _player.GetNode<Spatial>("Head"));

        Input.MouseMode = Input.MouseModeEnum.Captured;
        _printerUI.Visible = false;
    }
}