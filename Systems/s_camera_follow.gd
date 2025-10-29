# s_camera_follow.gd
class_name CameraFollowSystem
extends System

func query() -> QueryBuilder:
	return q.with_relationship([Relationship.new(C_CameraFollows.new(), null)])

func process(entity: Entity, delta: float) -> void:
	var camera_node: Camera3D = entity.get_node(".") as Camera3D
	var orbital_camera: C_OrbitalCamera = entity.get_component(C_OrbitalCamera)
	
	var follow_relationships = entity.get_relationships(Relationship.new(C_CameraFollows.new(), null))
	var target_e: Entity = follow_relationships[0].target
	var target_node: Node3D = target_e.get_node(".") as Node3D
	
	var world_shoulder_offset = target_node.global_transform.basis * orbital_camera.shoulder_offset
	var center_point = target_node.global_position + world_shoulder_offset
	
	var camera_pos = Vector3.ZERO
	camera_pos.x = center_point.x + orbital_camera.distance * sin(orbital_camera.yaw) * cos(orbital_camera.pitch)
	camera_pos.y = center_point.y + orbital_camera.distance * sin(orbital_camera.pitch)
	camera_pos.z = center_point.z + orbital_camera.distance * cos(orbital_camera.yaw) * cos(orbital_camera.pitch)
	
	camera_node.global_position = camera_pos
	camera_node.look_at(center_point)
