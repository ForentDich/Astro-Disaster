class_name InputPlayerSystem
extends System

func query() -> QueryBuilder:
	return q.with_all([C_Player, C_Velocity]) 
	
func process(entity: Entity, delta: float) -> void:
	var velocity_c = entity.get_component(C_Velocity)
	var jump_c = entity.get_component(C_Jump)

	var raw_input = Vector3.ZERO
	raw_input.z = Input.get_axis("move_forward","move_backward")
	raw_input.x = Input.get_axis("move_left","move_right")
	
	if raw_input.length() > 0.1:
		var camera_entities = ECS.world.query.with_relationship([Relationship.new(C_CameraFollows.new(), entity)]).execute()
		if camera_entities.size() > 0:
			var camera_entity = camera_entities[0]
			var orbital_camera: C_OrbitalCamera = camera_entity.get_component(C_OrbitalCamera)
			
			var camera_forward = -Vector3.FORWARD.rotated(Vector3.UP, orbital_camera.yaw)
			var camera_right = Vector3.RIGHT.rotated(Vector3.UP, orbital_camera.yaw)
			
			var world_direction = Vector3.ZERO
			world_direction += camera_forward * raw_input.z
			world_direction += camera_right * raw_input.x
			
			velocity_c.direction = world_direction.normalized()
	else:
		velocity_c.direction = raw_input
	
	# Прыжок
	if Input.is_action_just_pressed("jump"):
		jump_c.jump_buffer_timer = jump_c.jump_buffer_duration
