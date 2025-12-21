class_name GravitySystem
extends System

func query() -> QueryBuilder:
	return q.with_all([C_Gravity, C_Velocity])

func process(entity, delta: float) -> void:
	var gravity_c = entity.get_component(C_Gravity)
	var velocity_c = entity.get_component(C_Velocity)
	var noclip_c = entity.get_component(C_Noclip)
	var characterBody : CharacterBody3D = entity.get_node(".") as CharacterBody3D
	
	# Пропускаем гравитацию в режиме ноуклипа
	if noclip_c and noclip_c.is_active:
		return
	
	# Обычная логика гравитации
	if not characterBody.is_on_floor():
		velocity_c.velocity.y -= gravity_c.force * delta
		velocity_c.velocity.y = max(velocity_c.velocity.y, -gravity_c.max_fall_speed)
