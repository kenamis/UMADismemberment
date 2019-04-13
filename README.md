# UMADismemberment
Examples Scene -> “Assets/Dismemberment/Scene/Example”

The required component is “UMA Dismemberment”

![component](UMADismemberment/images/image01.jpg?raw=true "component")

Use Events – *Turn on or off the Dismembered Event.*

Slice Fill – *The material to be used to cap the sliced area.*

Global Threshold – *The total weight threshold to include a vertex in the slice or not.*

Sliceable Human Bones – *The human bones (and all its children) that are sliceable. Each bone can have an individual slice threshold too.*
	 
Dismembered Event – *This is invoked when the attached object has been sliced.  It passes the new root object transform and the transform of the bone that was sliced off.  This event can be used to attach game specific data or components and add special effects (like gibs or blood spray).
Calling “UMADismemberment.Slice” also has an out struct with the sliced information if the event is not desired.
There is an example script, “ExampleDismemberCallback”, that shows using this event to add a rigidbody and collider.*
