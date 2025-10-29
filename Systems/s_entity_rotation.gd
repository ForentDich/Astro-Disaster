class_name EntityRotationSystem
extends System

@export var rotation_speed: float = 5

func query() -> QueryBuilder:
	return q.with_all([C_Velocity]) 

func process(entity: Entity, delta: float) -> void:
	var velocity_c: C_Velocity = entity.get_component(C_Velocity)
	var entity_node: Node3D = entity.get_node(".")
	
	if velocity_c.direction.length() > 0.1:
		var target_yaw = atan2(velocity_c.direction.x, velocity_c.direction.z)
		entity_node.rotation.y = lerp_angle(entity_node.rotation.y, target_yaw, rotation_speed * delta)
