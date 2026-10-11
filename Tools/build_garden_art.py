"""Run through Blender MCP or: blender --background --python Tools/build_garden_art.py.
Creates an isolated scene, leaves the user's existing scene untouched.
"""
import bpy, math, os
from mathutils import Vector
ROOT = os.environ.get('CRATE_GARDEN_ROOT', os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, 'My project/Assets/Sokoban/Resources/GardenArt')
os.makedirs(OUT, exist_ok=True)
original = bpy.context.window.scene if bpy.context.window else None
scene = bpy.data.scenes.new('CrateGarden_Art')
if bpy.context.window: bpy.context.window.scene = scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 32
scene.cycles.use_denoising = True
scene.render.resolution_x = scene.render.resolution_y = 256
scene.render.resolution_percentage = 100
scene.render.film_transparent = True
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.view_settings.view_transform = 'Standard'
scene.world = bpy.data.worlds.new('Garden_StudioWorld')
scene.world.use_nodes = True
next(n for n in scene.world.node_tree.nodes if n.type == 'BACKGROUND').inputs[0].default_value = (.42,.52,.58,1)
next(n for n in scene.world.node_tree.nodes if n.type == 'BACKGROUND').inputs[1].default_value = .45

def material(name, color, metallic=0):
    m=bpy.data.materials.new('Garden_'+name); m.diffuse_color=(*color,1); m.use_nodes=True
    p=next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'); p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Roughness'].default_value=.38; p.inputs['Metallic'].default_value=metallic
    return m
wood=material('HoneyOak',(.61,.30,.08)); trim=material('OakEdge',(.95,.62,.24)); grain=material('WoodGrain',(.39,.17,.055))
metal=material('Brass',(.27,.19,.08),.55); green=material('DeliveredMint',(.25,.66,.42)); pale=material('MintEdge',(.57,.91,.68))
stone=material('Slate',(.15,.27,.28)); top=material('MossStone',(.24,.40,.35)); seam=material('SlateSeam',(.085,.16,.17))
cream=material('Porcelain',(.86,.89,.76)); visor=material('Visor',(.035,.12,.15),.3); eye=material('Eyes',(.40,.95,.75)); boot=material('Boots',(.13,.30,.32))
assets={}
active=[]
def cube(name,loc,scale,mat,bevel=.045):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc); o=bpy.context.object; o.name='Garden_'+name; o.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(mat)
    if bevel:
        b=o.modifiers.new('Soft manufactured edges','BEVEL'); b.width=bevel; b.segments=3
        o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    active.append(o); return o

def sphere(name,loc,scale,mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=16,location=loc); o=bpy.context.object; o.name='Garden_'+name; o.scale=scale; o.data.materials.append(mat)
    for p in o.data.polygons: p.use_smooth=True
    active.append(o); return o

def crate(delivered=False):
    a,b=(green,pale) if delivered else (wood,trim)
    cube('CrateCore',(0,0,.38),(1.18,1.12,.76),a,.065)
    for y in [-.44,.44]: cube('TopRail',(0,y,.79),(1.26,.14,.14),b,.025)
    for x in [-.55,.55]: cube('TopRail',(x,0,.79),(.14,.99,.14),b,.025)
    for x in [-.27,0,.27]: cube('PlankJoint',(x,0,.769),(.014,.78,.012),grain if not delivered else green,.002)
    brace=cube('DiagonalBrace',(0,0,.81),(.12,1.03,.08),b,.018); brace.rotation_euler.z=-math.pi/4
    for x in [-.52,.52]:
        cube('FrontBand',(x,-.59,.37),(.13,.10,.76),b,.018)
        for z in [.1,.65]: sphere('Rivet',(x,-.652,z),(.034,.017,.034),metal)
    for z in [.05,.69]: cube('FrontRail',(0,-.6,z),(1.16,.08,.12),b,.018)

for name in ['crate','crate_goal','wall','player']:
    active=[]
    if name.startswith('crate'): crate(name=='crate_goal')
    elif name=='wall':
        cube('StoneBase',(0,0,.22),(1.38,1.30,.44),stone,.08)
        cube('StoneCap',(0,0,.46),(1.39,1.31,.13),top,.055)
        cube('StoneJoint',(0,-.663,.20),(.025,.01,.26),seam,.002)
        for x in [-.44,.38]: cube('StoneInset',(x,-.667,.29),(.12,.018,.024),top,.006)
    else:
        for x in [-.25,.25]: sphere('Foot',(x,-.10,.12),(.19,.29,.13),boot)
        sphere('Body',(0,0,.38),(.44,.32,.37),boot)
        sphere('Helmet',(0,0,.78),(.57,.46,.46),cream)
        sphere('FaceGlass',(0,-.374,.79),(.43,.115,.23),visor)
        for x in [-.19,.19]: sphere('Eye',(x,-.48,.84),(.067,.03,.071),eye)
        for x in [-.53,.53]: sphere('Arm',(x,0,.43),(.13,.18,.24),cream)
        cube('Antenna',(0,0,1.27),(.045,.045,.20),metal,.012)
        sphere('AntennaTip',(0,0,1.38),(.09,.09,.09),green)
    assets[name]=active[:]
    for o in active: o.hide_render=True

camdata=bpy.data.cameras.new('Garden_RenderCamera'); cam=bpy.data.objects.new('Garden_RenderCamera',camdata); scene.collection.objects.link(cam)
cam.location=(0,-5.5,9); target=Vector((0,0,.48)); cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler(); camdata.type='ORTHO'; camdata.ortho_scale=1.85; scene.camera=cam
for name,loc,energy,size in [('Key',(-3,-4,7),450,4),('Fill',(4,1,5),220,3)]:
    data=bpy.data.lights.new('Garden_'+name,'AREA'); data.energy=energy; data.shape='DISK'; data.size=size
    o=bpy.data.objects.new('Garden_'+name,data); scene.collection.objects.link(o); o.location=loc; o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
for name,objects in assets.items():
    for o in objects: o.hide_render=False
    scene.render.filepath=os.path.join(OUT,name+'.png')
    bpy.ops.render.render(write_still=True,scene=scene.name)
    for o in objects: o.hide_render=True
# Arrange source models side-by-side for useful inspection.
for i,(name,objects) in enumerate(assets.items()):
    collection=bpy.data.collections.new('GardenAsset_'+name); scene.collection.children.link(collection)
    for o in objects:
        o.hide_render=False; o.location.x+=(i-1.5)*2
        for c in list(o.users_collection): c.objects.unlink(o)
        collection.objects.link(o)
bpy.data.libraries.write(os.path.join(ROOT,'ArtSource/GardenAssets.blend'),{scene},fake_user=True)
if original: bpy.context.window.scene=original
result={'assets':list(assets),'source':os.path.join(ROOT,'ArtSource/GardenAssets.blend'),'output':OUT}
