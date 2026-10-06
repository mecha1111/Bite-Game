extends SceneTree
# Development-only: regenerate the closed interior mask after editing gauge artwork.
# Runtime uses the saved resource; image decoding/flood fill never runs in gameplay.
func _initialize():
 var source=Image.load_from_file("res://assets/satiety_gauge.png")
 var mask=Image.create(source.get_width(),source.get_height(),false,Image.FORMAT_RGBA8)
 mask.fill(Color.TRANSPARENT)
 var queue:Array[Vector2i]=[Vector2i(1000,365)]
 mask.set_pixel(1000,365,Color.WHITE)
 var at=0
 while at<queue.size():
  var p=queue[at]
  at+=1
  for d in [Vector2i.LEFT,Vector2i.RIGHT,Vector2i.UP,Vector2i.DOWN]:
   var q=p+d
   if q.x<0 or q.y<0 or q.x>=source.get_width() or q.y>=source.get_height():continue
   if mask.get_pixelv(q).a>0 or source.get_pixelv(q).a>=0.5:continue
   mask.set_pixelv(q,Color.WHITE)
   queue.append(q)
 print("MASK interior=",mask.get_used_rect()," pixels=",queue.size())
 assert(mask.get_used_rect()==Rect2i(352,353,1429,73),"Gauge art changed: update shader/scene normalized bounds together.")
 assert(queue.size()<200000)
 assert(ResourceSaver.save(ImageTexture.create_from_image(mask),"res://game/gameplay/satiety_interior.res",ResourceSaver.FLAG_COMPRESS)==OK)
 quit()
