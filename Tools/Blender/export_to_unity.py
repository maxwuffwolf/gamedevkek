"""
Экспорт выделенных объектов Blender в Unity одним нажатием.

Как пользоваться (один раз):
  1. Blender → вкладка Scripting → Open → этот файл.
  2. Run Script (Alt+P). Появится панель: 3D-вид → N → вкладка «Unity».
     Чтобы панель была всегда: Edit → Preferences → Add-ons → Install from Disk → этот .py.

Что делает кнопка «Export selected»:
  * каждый выделенный меш → отдельный FBX `<имя объекта>.fbx`;
  * объект временно ставится в (0,0,0), чтобы префаб в Unity не съезжал;
  * настройки FBX как в docs/06: FBX All, -Z Forward, Y Up, Apply Transform, цвета вершин sRGB;
  * папка: Game/Assets/_Project/Art/Models/<подпапка> — ищется вверх от .blend-файла
    (.blend должен лежать внутри клона репозитория, например Art/Blender/Environment/).
"""

bl_info = {
    "name": "Gamedevkek: Export to Unity",
    "author": "gamedevkek",
    "version": (1, 0, 0),
    "blender": (4, 2, 0),
    "location": "View3D > Sidebar (N) > Unity",
    "category": "Import-Export",
}

import os
import bpy

UNITY_MODELS = os.path.join("Game", "Assets", "_Project", "Art", "Models")


def find_repo_root(start):
    d = os.path.dirname(start)
    while d and os.path.dirname(d) != d:
        if os.path.isdir(os.path.join(d, "Game", "Assets")):
            return d
        d = os.path.dirname(d)
    return None


class GDK_OT_export_selected(bpy.types.Operator):
    bl_idname = "gdk.export_selected"
    bl_label = "Export selected"
    bl_description = "Каждый выделенный меш → отдельный FBX в папку Unity"

    def execute(self, context):
        if not bpy.data.filepath:
            self.report({"ERROR"}, "Сначала сохраните .blend внутри репозитория (Art/Blender/...)")
            return {"CANCELLED"}
        root = find_repo_root(bpy.data.filepath)
        if root is None:
            self.report({"ERROR"}, "Не нашёл папку Game/Assets выше .blend-файла")
            return {"CANCELLED"}

        sub = context.scene.gdk_subfolder.strip().strip("/\\") or "Environment/Forest"
        out_dir = os.path.join(root, UNITY_MODELS, sub)
        os.makedirs(out_dir, exist_ok=True)

        meshes = [o for o in context.selected_objects if o.type == "MESH"]
        if not meshes:
            self.report({"WARNING"}, "Не выделено ни одного меша")
            return {"CANCELLED"}

        if context.mode != "OBJECT":
            bpy.ops.object.mode_set(mode="OBJECT")
        active = context.view_layer.objects.active

        for obj in meshes:
            saved = obj.location.copy()
            obj.location = (0.0, 0.0, 0.0)
            bpy.ops.object.select_all(action="DESELECT")
            obj.select_set(True)
            context.view_layer.objects.active = obj
            path = os.path.join(out_dir, obj.name + ".fbx")
            try:
                bpy.ops.export_scene.fbx(
                    filepath=path,
                    use_selection=True,
                    object_types={"MESH"},
                    apply_unit_scale=True,
                    apply_scale_options="FBX_SCALE_ALL",
                    axis_forward="-Z",
                    axis_up="Y",
                    bake_space_transform=True,
                    use_mesh_modifiers=True,
                    mesh_smooth_type="FACE",
                    colors_type="SRGB",
                    add_leaf_bones=False,
                    bake_anim=False,
                    path_mode="AUTO",
                )
            finally:
                obj.location = saved

        for obj in meshes:
            obj.select_set(True)
        context.view_layer.objects.active = active
        self.report({"INFO"}, f"Экспортировано {len(meshes)} → {out_dir}")
        return {"FINISHED"}


class GDK_PT_unity(bpy.types.Panel):
    bl_label = "Unity export"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Unity"

    def draw(self, context):
        col = self.layout.column()
        col.prop(context.scene, "gdk_subfolder", text="Папка")
        col.operator(GDK_OT_export_selected.bl_idname, icon="EXPORT")
        col.label(text="Перед экспортом: Ctrl+A → All Transforms")


classes = (GDK_OT_export_selected, GDK_PT_unity)


def register():
    bpy.types.Scene.gdk_subfolder = bpy.props.StringProperty(
        name="Подпапка", default="Environment/Forest",
        description="Подпапка внутри Game/Assets/_Project/Art/Models")
    for c in classes:
        bpy.utils.register_class(c)


def unregister():
    for c in reversed(classes):
        bpy.utils.unregister_class(c)
    del bpy.types.Scene.gdk_subfolder


if __name__ == "__main__":
    # повторный Run Script не должен падать с «already registered»
    try:
        unregister()
    except Exception:
        pass
    register()
