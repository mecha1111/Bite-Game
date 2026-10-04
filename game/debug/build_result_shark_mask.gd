## Developer-only preprocessing: source alpha flood-fill yields an enclosed body mask.
## Run explicitly with Godot --headless --path . --script res://game/debug/build_result_shark_mask.gd
extends SceneTree
func _initialize():
    var source=Image.load_from_file("res://assets/투명한 게이지 픽셀 상어.png")
    var w=source.get_width()
    var h=source.get_height()
    var alpha=source.get_data()
    var seen=PackedByteArray();seen.resize(w*h)
    var queue=PackedInt32Array()
    for x in range(w):queue.append(x);queue.append((h-1)*w+x)
    for y in range(h):queue.append(y*w);queue.append(y*w+w-1)
    var cursor=0
    while cursor<queue.size():
        var idx=queue[cursor];cursor+=1
        if seen[idx] or alpha[idx*4+3]>=128:continue
        seen[idx]=1
        var x=idx%w
        var y=idx/w
        if x>0 and not seen[idx-1]:queue.append(idx-1)
        if x<w-1 and not seen[idx+1]:queue.append(idx+1)
        if y>0 and not seen[idx-w]:queue.append(idx-w)
        if y<h-1 and not seen[idx+w]:queue.append(idx+w)
    var data=PackedByteArray();data.resize(w*h*4)
    var count=0
    for idx in range(w*h):
        if not seen[idx] and alpha[idx*4+3]<128:
            for channel in range(4):data[idx*4+channel]=255
            count+=1
    var mask=Image.create_from_data(w,h,false,Image.FORMAT_RGBA8,data)
    var result=ResourceSaver.save(ImageTexture.create_from_image(mask),"res://game/gameplay/result_shark_interior.res",ResourceSaver.FLAG_COMPRESS)
    print("RESULT MASK: ",count," enclosed pixels, save=",result)
    quit(result)
