class_name MovementSystem
extends System

func query() -> QueryBuilder:
	return q.with_all([C_Velocity])
	
func process(entity: Entity, delta: float) -> void:
	var velocity_c : C_Velocity = entity.get_component(C_Velocity)
	var noclip_c = entity.get_component(C_Noclip)
	var characterBody : CharacterBody3D = entity.get_node(".") as CharacterBody3D

	var is_noclip = noclip_c and noclip_c.is_active
	
	# Если в ноуклипе - отключаем гравитацию и столкновения
	if is_noclip:
		# Отключаем гравитацию
		
		# Применяем движение по всем осям
		var current_speed = velocity_c.speed * noclip_c.speed_multiplier
		
		# Горизонтальное движение
		velocity_c.velocity.x = velocity_c.direction.x * current_speed
		velocity_c.velocity.z = velocity_c.direction.z * current_speed
		
		# Вертикальное движение (если есть направление по Y)
		velocity_c.velocity.y = velocity_c.direction.y * noclip_c.vertical_speed
		
		# Если нет вертикального ввода - останавливаемся по Y
		if abs(velocity_c.direction.y) < 0.1:
			velocity_c.velocity.y = 0.0
		
		# Если ноуклип позволяет пролетать сквозь стены - устанавливаем режим
		if noclip_c.can_fly_through_walls:
			characterBody.collision_mask = 0  # Отключаем все слои коллизии
	else:
		# Обычное движение с гравитацией
		characterBody.collision_mask = 1  # Включаем стандартный слой коллизии
		
		# Только горизонтальное движение, вертикальное оставляем для гравитации
		velocity_c.velocity.x = velocity_c.direction.x * velocity_c.speed
		velocity_c.velocity.z = velocity_c.direction.z * velocity_c.speed
		# velocity_c.velocity.y оставляем без изменений (обрабатывается в GravitySystem)
	
	# Применяем движение
	characterBody.velocity = velocity_c.velocity
	characterBody.move_and_slide()
	
	# Обновляем velocity из CharacterBody3D
	velocity_c.velocity = characterBody.velocity
	
	# Дебаг-информация
	if is_noclip and Input.is_action_pressed("shift"):
		# Ускорение при зажатом Shift
		velocity_c.velocity = velocity_c.velocity * 2.0
