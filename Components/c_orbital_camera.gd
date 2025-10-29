# c_orbital_camera.gd
class_name C_OrbitalCamera
extends Component

@export var distance: float = 3.0
@export var shoulder_offset: Vector3 = Vector3(-0.5, 1.5, 0)
@export var sensitivity: float = 0.002

var yaw: float = 0.0
var pitch: float = 0.0
