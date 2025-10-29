# s_input_camera.gd
class_name CameraInputSystem
extends System

var mouse_motion_accumulated := Vector2.ZERO
var target_distance: float = -1.0
var distance_lerp_speed: float = 8

func _ready():
	Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED)

func _input(event):
	if event is InputEventMouseMotion and Input.get_mouse_mode() == Input.MOUSE_MODE_CAPTURED:
		mouse_motion_accumulated = event.relative
	
	if event is InputEventMouseButton and Input.get_mouse_mode() == Input.MOUSE_MODE_CAPTURED:
		match event.button_index:
			MOUSE_BUTTON_WHEEL_UP:
				adjust_camera_distance(-0.5)
			MOUSE_BUTTON_WHEEL_DOWN:
				adjust_camera_distance(0.5)
	
	if Input.is_action_just_pressed("ui_cancel"):
		if Input.get_mouse_mode() == Input.MOUSE_MODE_CAPTURED:
			Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)
		else:
			Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED)

func query() -> QueryBuilder:
	return q.with_all([C_OrbitalCamera])

func process(entity: Entity, delta: float) -> void:
	var orbital_camera: C_OrbitalCamera = entity.get_component(C_OrbitalCamera)
	
	if mouse_motion_accumulated != Vector2.ZERO:
		orbital_camera.yaw += -mouse_motion_accumulated.x * orbital_camera.sensitivity
		orbital_camera.pitch += mouse_motion_accumulated.y * orbital_camera.sensitivity
		orbital_camera.pitch = clamp(orbital_camera.pitch, -PI/4, PI/3)
		mouse_motion_accumulated = Vector2.ZERO
	

	if target_distance >= 0:
		orbital_camera.distance = lerp(orbital_camera.distance, target_distance, distance_lerp_speed * delta)
		
		if abs(orbital_camera.distance - target_distance) < 0.05:
			orbital_camera.distance = target_distance
			target_distance = -1

func adjust_camera_distance(amount: float):
	var entities = ECS.world.query.with_all([C_OrbitalCamera]).execute()
	var orbital_camera: C_OrbitalCamera = entities[0].get_component(C_OrbitalCamera)
	target_distance = clamp(orbital_camera.distance + amount, 1.5, 8.0)
