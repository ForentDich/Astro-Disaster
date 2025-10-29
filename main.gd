extends Node

@onready var world: World = $World

func _ready() -> void:
	ECS.world = world
	
	await get_tree().process_frame
	var player : Entity = ECS.world.get_entity_by_id('PlayerMain')
	var camera : Camera = ECS.world.get_entity_by_id('CameraMain')
	
	camera.add_relationship(Relationship.new(C_CameraFollows.new(), player))

func _process(delta: float) -> void:
	if ECS.world:
		ECS.process(delta)

	
	
	
