class_name JumpSystem
extends System

func query() -> QueryBuilder:
	return q.with_all([C_Jump, C_Velocity])
	
func process(entity: Entity, delta: float) -> void:
	var jump_c = entity.get_component(C_Jump)
	var velocity_c = entity.get_component(C_Velocity)
	var characterBody : CharacterBody3D = entity.get_node(".") as CharacterBody3D
	
	if jump_c.jump_buffer_timer > 0:
		jump_c.jump_buffer_timer -= delta
	
	if jump_c.jump_buffer_timer > 0 and characterBody.is_on_floor():
		velocity_c.velocity.y = jump_c.jump_force
		jump_c.jump_buffer_timer = 0
