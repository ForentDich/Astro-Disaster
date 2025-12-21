class_name InputPlayerSystem
extends System

func query() -> QueryBuilder:
	return q.with_all([C_Player, C_Velocity]) 
	
func process(entity: Entity, delta: float) -> void:
	var velocity_c = entity.get_component(C_Velocity)
	var jump_c = entity.get_component(C_Jump)
	var noclip_c = entity.get_component(C_Noclip)

	var raw_input = Vector3.ZERO
	raw_input.z = Input.get_axis("move_forward","move_backward")
	raw_input.x = Input.get_axis("move_left","move_right")

	# Переключение ноуклипа
	if Input.is_action_just_pressed("v"):
		if noclip_c:
			noclip_c.is_active = !noclip_c.is_active
			print("Noclip: ", "ON" if noclip_c.is_active else "OFF")
		else:
			# Создаем компонент при первом использовании
			noclip_c = C_Noclip.new()
			entity.add_component(noclip_c)
			noclip_c.is_active = true
			print("Noclip: ON")
	
	# В ноуклипе добавляем вертикальное движение
	if noclip_c and noclip_c.is_active:
		if Input.is_action_pressed("jump"):
			raw_input.y = 25.0  # Вверх
		elif Input.is_action_pressed("crouch"):
			raw_input.y = -25.0  # Вниз
	
	if raw_input.length() > 0.1:
		var camera_entities = ECS.world.query.with_relationship([Relationship.new(C_CameraFollows.new(), entity)]).execute()
		if camera_entities.size() > 0:
			var camera_entity = camera_entities[0]
			var orbital_camera: C_OrbitalCamera = camera_entity.get_component(C_OrbitalCamera)
			
			var camera_forward = -Vector3.FORWARD.rotated(Vector3.UP, orbital_camera.yaw)
			var camera_right = Vector3.RIGHT.rotated(Vector3.UP, orbital_camera.yaw)
			var camera_up = Vector3.UP
			
			var world_direction = Vector3.ZERO
			world_direction += camera_forward * raw_input.z
			world_direction += camera_right * raw_input.x
			
			# В ноуклипе добавляем вертикальное движение по камере
			if noclip_c and noclip_c.is_active:
				world_direction += camera_up * raw_input.y
			
			velocity_c.direction = world_direction.normalized()
	else:
		velocity_c.direction = raw_input
	
	# Прыжок (только не в ноуклипе)
	if Input.is_action_just_pressed("jump") and not (noclip_c and noclip_c.is_active):
		jump_c.jump_buffer_timer = jump_c.jump_buffer_duration
