class_name C_Gravity
extends Component

@export var direction : Vector3 = Vector3.DOWN
@export var force : float = 9.8
@export var max_fall_speed : float = 20
#@export var enabled : bool = true

var gravity: Vector3:
	get:
		return direction * force
