class_name MovementSystem
extends System

func query() -> QueryBuilder:
	return q.with_all([C_Velocity])
	
func process(entity: Entity, delta: float) -> void:
	var velocity_c : C_Velocity = entity.get_component(C_Velocity)
	var characterBody : CharacterBody3D = entity.get_node(".") as CharacterBody3D
	var current_y = velocity_c.velocity.y
	
	velocity_c.velocity.x = velocity_c.direction.x * velocity_c.speed
	velocity_c.velocity.z = velocity_c.direction.z * velocity_c.speed
	velocity_c.velocity.y = current_y
	
	characterBody.velocity = velocity_c.velocity
	characterBody.move_and_slide()
	
	velocity_c.velocity = characterBody.velocity
