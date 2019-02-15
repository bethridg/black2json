using System.Linq;
using System.Dynamic;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using System.CodeDom.Compiler;
using System.CodeDom;
using System.Reflection;

using System.Windows.Forms;

namespace Red
{
    public partial class BlackObject : DynamicObject
    {
        string[] Names;
        string[] NamesUC;
        Dictionary<int, BlackObject> objects = new Dictionary<int, BlackObject>();
        string Type;
        BlackObject Parent = null;
        BlackObject Root = null;
        private string fname;

        public BlackObject(Stream stream, string name = null)
        {
            fname = name;
            using (var br = new BinaryReader(stream))
            {
                var tmp = br.ReadUInt64();
                var len = br.ReadUInt32();
                var arrlen = br.ReadUInt16();
                var bytes = br.ReadBytes((int)len - 2);

                Names = Encoding.ASCII.GetString(bytes).Split('\0');
                len = br.ReadUInt32();
                arrlen = br.ReadUInt16();
                bytes = br.ReadBytes((int)len - 2);
                NamesUC = Encoding.Unicode.GetString(bytes).Split('\0');

                var index = br.ReadInt32();
                len = br.ReadUInt32();
                var start = br.BaseStream.Position;

                Create(br, index, len, start, null, this);
            }
        }

        long startInFile = 0;
        long lenInFile = 0;

        BlackObject(BinaryReader br, int? index, long len, long start, BlackObject parent, BlackObject root, bool skip = false)
        {
            Create(br, index, len, start, parent, root, skip);
        }
        void Create(BinaryReader br, int? index, long len, long start, BlackObject parent, BlackObject root, bool skip = false)
        {
            Root = root;
            Parent = parent;
            var typeIndex = br.ReadInt16();
            Type = root.Names[typeIndex];

            if (index.HasValue)
            {
                root.objects[index.Value] = this;
                dictionary.Add("__i", index);
            }

            dictionary.Add("__t", Type);

            if (skip)
            {
                br.BaseStream.Position = start + len;
                dictionary.Add("__skipped", true);
                return;
            }

            startInFile = start;
            lenInFile = len;

            while (br.BaseStream.Position < len + start)
            {
                var nameIndex = br.ReadInt16();
                var name = root.Names[nameIndex];

                if (name == "object" || name == "operator" || name == "")
                {
                    name += '_';
                }

                //                if (name == "timeOffset")
                //                {
                //
                //                }

                var key = new MK(Type, name);
                if (Readers.Keys.Contains(key))
                {
                    if (!dictionary.ContainsKey(name))
                    {
                        dictionary.Add(name, Readers[key].Item2(br, this, root, null));
                    }
                    else
                    {
                        Readers[key].Item2(br, this, root, null);
                        if (!(new string[] { "AudEmitterPersisted", "AudEmitter" }.Contains(Type) && name == "name"))
                            throw new ArgumentException();
                    }
                    continue;
                }

                var pos = br.BaseStream.Position;
                var value = br.ReadInt16();

                if (br.BaseStream.Position < len + start)
                {
                    var peek = br.ReadInt16(); // peek ahead

                    if (peek == 0)
                    {
                        br.BaseStream.Position = pos; // move pointer back prior to peek
                        var ind = br.ReadInt32();

                        var notAnArray = new string[] { "emitParticleDuringLifeEmitter", "rotationCurve", "emissiveCurve", "scalingCurve", "transparentFlareMaterial", "flareMaterial", "kelvinColor", "noiseScaleCurve", "length", "observer", "transformBase", "lowDetailMesh", "mediumDetailMesh", "sourceObject", "destinationObject", "Tr2InstancedMesh", "instanceGeometryResource", "particleSystem", "mesh", "YCurve", "ZCurve", "XCurve", "decalEffect", "effect", "BlueCurve", "GreenCurve", "RedCurve", "AlphaCurve", "shader", "turretEffect" };
                        var sureAnArray = new string[] { "permuteTags", "parameterDescriptions", "transparentFlareData", "flareData", "lights", "systems", "enlightenAreas", "meshes", "transparentAreas", "subEmitters", "warheads", "flares", "stretch", "keys", "curveSets", "additiveEffects", "opaqueAreas", "children", "parameters", "damageLocators", "curves", "functions", "generators", "locators", "passes2Stage", "shaderMaterials", "areas" };

                        if (!sureAnArray.Contains(name) && ind == root.objects.Last().Key + 1)
                        {
                            dictionary.Add(name, ObjectReader(br, this, root, ind));
                        }
                        else if (notAnArray.Contains(name) && ind <= root.objects.Last().Key) //not an array so refference
                        {
                            dictionary.Add(name, CreateRef(ind));
                        }
                        else
                        {
                            dictionary.Add(name, ArrayReader(br, this, root, ind));
                        }
                    }
                    else
                    {
                        if (!dictionary.ContainsKey(name))
                        {
                            dictionary.Add(name, root.Names[value]);
                        }
                        else
                        {
                            if (!(new string[] { "AudEmitterPersisted", "AudEmitter" }.Contains(Type) && name == "name"))
                                throw new ArgumentException();
                        }
                        br.BaseStream.Position = pos + 2;
                    }
                }
                else
                {
                    if (!dictionary.Keys.Contains(name))
                    {
                        dictionary.Add(name, root.Names[value]);
                    }
                    else
                    {
                        if (!(new string[] { "AudEmitterPersisted", "AudEmitter" }.Contains(Type) && name == "name"))
                            throw new ArgumentException();
                    }
                }
            }
        }

        BlackObject()
        {

        }

        BlackObject CreateRef(int index, bool real = false)
        {
            if (real)
                return Root.objects[index];
            var ret = new BlackObject();
            ret.dictionary["__t"] = "_Ref";
            ret.dictionary["__ri"] = index;
            ret.Root = Root;
            ret.Parent = Parent;
            return ret;
        }


        public class MK : Tuple<string, string>
        {
            public MK(string Type, string Name) : base(Type, Name) { }
        }
        public class MKF : Tuple<MK, Reader>
        {
            public MKF(MK Key, Reader ReaderFunction) : base(Key, ReaderFunction) { }
            public MKF(string Type, string Name, Reader ReaderFunction) : base(new MK(Type, Name), ReaderFunction) { }
        }
        public delegate object Reader(BinaryReader br, BlackObject me, BlackObject root, int? index);


        static readonly Reader Float5Reader = (br, me, root, index) => new float[] { br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle() };
        static readonly Reader Float4Reader = (br, me, root, index) => new float[] { br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle() };
        static readonly Reader Float3Reader = (br, me, root, index) => new float[] { br.ReadSingle(), br.ReadSingle(), br.ReadSingle() };
        static readonly Reader Float2Reader = (br, me, root, index) => new float[] { br.ReadSingle(), br.ReadSingle() };
        static readonly Reader DoubleReader = (br, me, root, index) => br.ReadDouble();
        static readonly Reader Int64Reader = (br, me, root, index) => br.ReadInt64();
        static readonly Reader FloatReader = (br, me, root, index) => br.ReadSingle();
        static readonly Reader Int16Reader = (br, me, root, index) => br.ReadInt16();
        static readonly Reader Int32Reader = (br, me, root, index) => br.ReadInt32();
        static readonly Reader BoolReader = (br, me, root, index) => (br.ReadByte() == 1);
        static readonly Reader NameReader = (br, me, root, index) => root.Names[br.ReadInt16()];
        static readonly Reader NameUCReader = (br, me, root, index) => root.Names[br.ReadInt16()];
        static readonly Reader ObjectWithoutIndexReader = (br, me, root, index) => new BlackObject(br, index, br.ReadInt32(), br.BaseStream.Position, me, root);
        static readonly Reader DebugThrow = (br, me, root, index) => { throw new Exception(); };
        static readonly Reader Matrix4x4Reader = (br, me, root, index) =>
        {
            var matrix = new float[16];
            for (int i = 0; i < 16; i++)
                matrix[i] = br.ReadSingle();
            return matrix;
        };


        static readonly Reader IndexBufferReader = (br, me, root, index) =>
        {
            if (!index.HasValue)
                index = br.ReadInt32(); // buffer length

            var arr = new List<dynamic>((int)index);

            for (int i = 0; i < index; i++)
            {
                arr.Add(FloatReader(br, me, root, index));
            }

            br.ReadInt16(); // end of buffer?

            return arr;
        };

        static readonly Reader SkipObjectReader = (br, me, root, index) =>
        {
            if (!index.HasValue)
                index = br.ReadInt32();
            if (index == root.objects.Last().Key + 1)
            {
                return new BlackObject(br, index, br.ReadUInt32(), br.BaseStream.Position, me, root, true);
            }
            return me.CreateRef(index.Value);
        };
        static readonly Reader ObjectReader = (br, me, root, index) =>
        {
            if (!index.HasValue)
                index = br.ReadInt32();
            if (index == root.objects.Last().Key + 1)
                return new BlackObject(br, index, br.ReadUInt32(), br.BaseStream.Position, me, root);
            return me.CreateRef(index.Value);
        };
        static readonly Reader DictionaryReader = (br, me, root, index) =>
        {
            Dictionary<string, dynamic> ret = new Dictionary<string, dynamic>();
            int count = br.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var ind = br.ReadInt16();
                var newindex = br.ReadInt32();
                if (newindex == root.objects.Last().Key + 1)
                    ret[me.Root.Names[ind]] = ObjectReader(br, me, root, newindex);
                else
                    ret[me.Root.Names[ind]] = me.CreateRef(newindex);

            }
            return ret;
        };

        static readonly Reader Array16Reader = (br, me, root, index) =>
        {
            if (!index.HasValue)
                index = br.ReadInt32();

            var arr = new List<dynamic>((int)index);

            for (int i = 0; i < index; i++)
            {
                var newindex = br.ReadInt32();

                if (newindex <= root.objects.Last().Key)
                    arr.Add(me.CreateRef(newindex));
                else if (newindex == root.objects.Last().Key + 1)
                    arr.Add(ObjectReader(br, me, root, newindex));
                else
                    throw new Exception("Array len > array real elemns!");

            }
            return arr;
        };

        static readonly Reader ArrayReader = (br, me, root, index) =>
        {
            if (!index.HasValue)
                index = br.ReadInt32();

            var arr = new List<dynamic>((int)index);

            for (int i = 0; i < index; i++)
            {
                var newindex = br.ReadInt32();

                if (newindex <= root.objects.Last().Key)
                    arr.Add(me.CreateRef(newindex));
                else if (newindex == root.objects.Last().Key + 1)
                    arr.Add(ObjectReader(br, me, root, newindex));//new BlackObject(br, newindex, br.ReadUInt32(), br.BaseStream.Position, me, root));
                else
                    throw new Exception("Array len > array real elemns!");
            }
            return arr;
        };
        static readonly Reader StaticParams = (br, me, root, index) =>
        {
            if (!index.HasValue)
                index = br.ReadInt32();
            var arr = new List<dynamic>((int)index);
            var size = br.ReadInt16();
            for (int i = 0; i < index; i++)
            {
                arr.Add(new { name = root.Names[br.ReadInt32()], value = Float5Reader(br, me, root, null) });
            }
            return arr;
        };
        static readonly Reader damageLocators = (br, me, root, index) =>
        {
            var index1 = br.ReadUInt32();
            var arr = new List<float[]>((int)index1);
            br.ReadInt16();
            for (int j = 0; j < index1; j++)
            {

                var matrix = new float[7];
                for (int i = 0; i < 7; i++)
                    matrix[i] = br.ReadSingle();
                arr.Add(matrix);
            }
            return arr;
        };

        public static void AddToReadersDictionary(MKF mkf)
        {
            Readers.Add(mkf.Item1, mkf);
        }
        public static readonly Dictionary<MK, MKF> Readers =

        new MKF[] {
new MKF("EveSOFDataHull", "description", NameReader),
new MKF("EveSOFDataHull", "geometryResFilePath", NameReader),
new MKF("EveSOFDataHull", "boundingSphere", Float4Reader),
new MKF("EveSOFDataHull", "opaqueAreas", ArrayReader),
new MKF("EveSOFDataHull", "spriteSets", ArrayReader),
new MKF("EveSOFDataHull", "hullDecals", ArrayReader),
new MKF("EveSOFDataHull", "spotlightSets", ArrayReader),
new MKF("EveSOFDataHull", "planeSets", ArrayReader),
new MKF("EveSOFDataHull", "additiveAreas", ArrayReader),
new MKF("EveSOFDataHull", "distortionAreas", ArrayReader),
new MKF("EveSOFDataHull", "transparentAreas", ArrayReader),
new MKF("EveSOFDataHull", "depthAreas", ArrayReader),
new MKF("EveSOFDataHull", "children", ArrayReader),
new MKF("EveSOFDataHull", "name", NameReader),
new MKF("EveSOFDataHull", "booster", ObjectReader),





// added
new MKF("EveSOFDataHull", "shapeEllipsoidCenter", Float3Reader),
new MKF("EveSOFDataHull", "shapeEllipsoidRadius", Float3Reader),
new MKF("EveSOFDataHull", "category", NameReader),
new MKF("EveSOFDataHull", "decalSets", ArrayReader),
new MKF("EveSOFDataHull", "impactEffectType", Int32Reader),
new MKF("EveSOFDataHull", "audioPosition", Float3Reader),
new MKF("EveSOFDataHull", "isSkinned", BoolReader),
// end


// added
new MKF("EveSOFDataHullLocator", "transform", Matrix4x4Reader),
// end


// added
new MKF("EveSOFDataTransform", "position", Float3Reader),
new MKF("EveSOFDataTransform", "rotation", Float4Reader),
new MKF("EveSOFDataTransform", "boneIndex", Int32Reader),
// end





new MKF("EveSOFDataHullArea", "index", Int32Reader),
new MKF("EveSOFDataHullArea", "name", NameReader),
new MKF("EveSOFDataHullArea", "shaderPath", NameReader),
new MKF("EveSOFDataHullArea", "textures", ArrayReader),
new MKF("EveSOFDataHullArea", "parameters", ArrayReader),

new MKF("EveSOFDataTexture", "resFilePath", NameReader),
new MKF("EveSOFDataTexture", "name", NameReader),

new MKF("EveSOFDataParameter", "name", NameReader),
new MKF("EveSOFDataParameter", "value", Float4Reader),

new MKF("EveSOFDataHullPlaneSet", "layer1MapResPath", NameReader),
new MKF("EveSOFDataHullPlaneSet", "layer2MapResPath", NameReader),
new MKF("EveSOFDataHullPlaneSet", "maskMapResPath", NameReader),
new MKF("EveSOFDataHullPlaneSet", "planeData", Float4Reader),
new MKF("EveSOFDataHullPlaneSet", "items", ArrayReader),

// added
new MKF("EveSOFDataHullPlaneSet", "usage", Int32Reader),
// end

new MKF("EveSOFDataHullPlaneSetItem", "position", Float3Reader),
new MKF("EveSOFDataHullPlaneSetItem", "rotation", Float4Reader),
new MKF("EveSOFDataHullPlaneSetItem", "scaling", Float3Reader),
new MKF("EveSOFDataHullPlaneSetItem", "color", Float4Reader),
new MKF("EveSOFDataHullPlaneSetItem", "layer1Transform", Float4Reader),
new MKF("EveSOFDataHullPlaneSetItem", "layer1Scroll", Float4Reader),
new MKF("EveSOFDataHullPlaneSetItem", "layer2Transform", Float4Reader),
new MKF("EveSOFDataHullPlaneSetItem", "layer2Scroll", Float4Reader),
new MKF("EveSOFDataHullPlaneSetItem", "boneIndex", Int32Reader),

// added
new MKF("EveSOFDataHullPlaneSetItem", "groupIndex", Int32Reader),
new MKF("EveSOFDataHullPlaneSetItem", "decalSets", ArrayReader),
// end



new MKF("EveSOFDataHullSpotlightSet", "zOffset", FloatReader),
new MKF("EveSOFDataHullSpotlightSet", "coneTextureResPath", NameReader),
new MKF("EveSOFDataHullSpotlightSet", "glowTextureResPath", NameReader),
new MKF("EveSOFDataHullSpotlightSet", "items", ArrayReader),
new MKF("EveSOFDataHullSpotlightSet", "transform", Matrix4x4Reader),
new MKF("EveSOFDataHullSpotlightSet", "groupIndex", Int32Reader),
new MKF("EveSOFDataHullSpotlightSet", "spriteScale", Float3Reader),

new MKF("EveSOFDataHullSpotlightSetItem", "transform", Matrix4x4Reader),
new MKF("EveSOFDataHullSpotlightSetItem", "groupIndex", Int32Reader),
new MKF("EveSOFDataHullSpotlightSetItem", "spriteScale", Float3Reader),
new MKF("EveSOFDataHullSpotlightSetItem", "coneIntensity", FloatReader),
new MKF("EveSOFDataHullSpotlightSetItem", "flareIntensity", FloatReader),
new MKF("EveSOFDataHullSpotlightSetItem", "spriteIntensity", FloatReader),





new MKF("EveSOFDataHullDecal", "position", Float3Reader),
new MKF("EveSOFDataHullDecal", "rotation", Float4Reader),
new MKF("EveSOFDataHullDecal", "scaling", Float3Reader),
new MKF("EveSOFDataHullDecal", "shaderPath", NameReader),
new MKF("EveSOFDataHullDecal", "parameters", ArrayReader),
new MKF("EveSOFDataHullDecal", "textures", ArrayReader),



// added
new MKF("EveSOFDataHullDecalSet", "items", ArrayReader),
new MKF("EveSOFDataHullDecalSetItem", "name", NameReader),
new MKF("EveSOFDataHullDecalSetItem", "usage", Int32Reader),
new MKF("EveSOFDataHullDecalSetItem", "position", Float3Reader),
new MKF("EveSOFDataHullDecalSetItem", "rotation", Float4Reader),
new MKF("EveSOFDataHullDecalSetItem", "scaling", Float3Reader),
new MKF("EveSOFDataHullDecalSetItem", "meshIndex", Int32Reader),
new MKF("EveSOFDataHullDecalSetItem", "indexBuffer", IndexBufferReader),
new MKF("EveSOFDataHullDecalSetItem", "glowColorType", Int32Reader),
new MKF("EveSOFDataHullDecalSetItem", "logoType", Int32Reader),


new MKF("EveSOFDataHullDecalSetItem", "boneIndex", Int32Reader),

// end



new MKF("EveSOFDataHullSpriteSet", "items", ArrayReader),
new MKF("EveSOFDataHullSpriteSet", "skinned", BoolReader),

new MKF("EveSOFDataHullSpriteSetItem", "boneIndex", Int32Reader),
new MKF("EveSOFDataHullSpriteSetItem", "position", Float3Reader),
new MKF("EveSOFDataHullSpriteSetItem", "blinkRate", FloatReader),
new MKF("EveSOFDataHullSpriteSetItem", "minScale", FloatReader),
new MKF("EveSOFDataHullSpriteSetItem", "maxScale", FloatReader),
new MKF("EveSOFDataHullSpriteSetItem", "groupIndex", Int32Reader),
new MKF("EveSOFDataHullSpriteSetItem", "blinkPhase", FloatReader),

new MKF("EveSOFDataHullSpotlightSetItem", "boosterGainInfluence", BoolReader),
new MKF("EveSOFDataHullSpotlightSetItem", "boneIndex", Int32Reader),



// added
new MKF("EveSOFDataHullSpriteSetItem", "colorType", Int32Reader),
// end




new MKF("EveSOFDataHullSpotlightSet", "skinned", BoolReader),

new MKF("EveSOFDataHullPlaneSet", "skinned", BoolReader),

new MKF("EveSOFDataHullSpriteSetItem", "falloff", FloatReader),

new MKF("EveSOFDataHullChild", "redFilePath", NameReader),
new MKF("EveSOFDataHullChild", "translation", Float3Reader),
new MKF("EveSOFDataHullChild", "scaling", Float3Reader),
new MKF("EveSOFDataHullChild", "rotation", Float4Reader),

new MKF("EveSOFData", "faction", ArrayReader),

new MKF("EveSOFDataFaction", "name", NameReader),
new MKF("EveSOFDataFaction", "description", NameReader),
new MKF("EveSOFDataFaction", "resPathInsert", NameReader),
new MKF("EveSOFDataFaction", "opaqueAreas", ArrayReader),

new MKF("EveSOFDataFactionHullArea", "name", NameReader),
new MKF("EveSOFDataFactionHullArea", "parameters", ArrayReader),

new MKF("EveSOFDataFaction", "transparentAreas", ArrayReader),
new MKF("EveSOFDataFaction", "spriteSets", ArrayReader),
new MKF("EveSOFDataFaction", "spotlightSets", ArrayReader),

new MKF("EveSOFDataFactionSpriteSet", "groupIndex", Int32Reader),
new MKF("EveSOFDataFactionSpriteSet", "color", Float4Reader),


new MKF("EveSOFDataFactionSpotlightSet", "groupIndex", Int32Reader),
new MKF("EveSOFDataFactionSpotlightSet", "coneColor", Float4Reader),
new MKF("EveSOFDataFactionSpotlightSet", "spriteColor", Float4Reader),
new MKF("EveSOFDataFactionSpotlightSet", "flareColor", Float4Reader),

new MKF("Tr2Effect", "constParameters", StaticParams),

new MKF("AudEmitter", "eventName", NameReader),
new MKF("AudEmitter", "name", NameReader),

new MKF("AudEmitterPersisted", "eventName", NameReader),
new MKF("AudEmitterPersisted", "name", NameReader),

new MKF("AudEventCurve", "extrapolation", Int32Reader),
new MKF("AudEventCurve", "name", NameReader),
new MKF("AudEventCurve", "keys", ArrayReader),
new MKF("AudEventCurve", "sourceTriObserver", ObjectReader),

new MKF("AudEventKey", "value", NameReader),
new MKF("AudEventKey", "time", FloatReader),

new MKF("BlueObjectProxy", "object_", ObjectReader),

new MKF("EveBoosterSet2", "alwaysOn", BoolReader),
new MKF("EveBoosterSet2", "destinyUpdate", BoolReader),
new MKF("EveBoosterSet2", "effect", ObjectReader),
new MKF("EveBoosterSet2", "glowColor", Float4Reader),
new MKF("EveBoosterSet2", "glows", ObjectReader),
new MKF("EveBoosterSet2", "glowScale", FloatReader),
new MKF("EveBoosterSet2", "haloColor", Float4Reader),
new MKF("EveBoosterSet2", "haloScaleX", FloatReader),
new MKF("EveBoosterSet2", "haloScaleY", FloatReader),
new MKF("EveBoosterSet2", "symHaloScale", FloatReader),
new MKF("EveBoosterSet2", "trails", ObjectReader),
new MKF("EveBoosterSet2", "trailsStaticOffsets1", Float3Reader),
new MKF("EveBoosterSet2", "trailsStaticOffsets2", Float3Reader),
new MKF("EveBoosterSet2", "trailsStaticOffsets3", Float3Reader),
new MKF("EveBoosterSet2", "trailsStaticOffsets4", Float3Reader),

new MKF("EveCamera", "fieldOfView", FloatReader),
new MKF("EveCamera", "friction", FloatReader),
new MKF("EveCamera", "frontClip", FloatReader),
new MKF("EveCamera", "idleMove", BoolReader),
new MKF("EveCamera", "idleScale", FloatReader),
new MKF("EveCamera", "idleSpeed", FloatReader),
new MKF("EveCamera", "intr", Float3Reader),
new MKF("EveCamera", "maxSpeed", FloatReader),
new MKF("EveCamera", "noiseScale", FloatReader),
new MKF("EveCamera", "noiseScaleCurve", ObjectReader),
new MKF("EveCamera", "pitch", FloatReader),
new MKF("EveCamera", "pos", Float3Reader),
new MKF("EveCamera", "rotationAroundParent", Float4Reader),
new MKF("EveCamera", "translationFromParent", FloatReader),
new MKF("EveCamera", "yaw", FloatReader),
new MKF("EveCamera", "zoomCurve", ObjectWithoutIndexReader),

new MKF("EveDustfieldConstraint", "maxStretch", FloatReader),
new MKF("EveDustfieldConstraint", "stretch", FloatReader),

new MKF("EveEffectRoot", "boundingSphereCenter", Float3Reader),
new MKF("EveEffectRoot", "boundingSphereRadius", FloatReader),
new MKF("EveEffectRoot", "duration", FloatReader),
new MKF("EveEffectRoot", "highDetail", ObjectReader),
new MKF("EveEffectRoot", "lowDetail", ObjectReader),
new MKF("EveEffectRoot", "mediumDetail", ObjectReader),
new MKF("EveEffectRoot", "name", NameReader),

new MKF("EveLensflare", "backgroundOccluders", ArrayReader),
new MKF("EveLensflare", "flares", ArrayReader),
new MKF("EveLensflare", "name", NameReader),
new MKF("EveLensflare", "occluders", ArrayReader),
new MKF("EveLensflare", "position", Float3Reader),

new MKF("EveLensflare", "distanceToEdgeCurves", ArrayReader),
new MKF("EveLensflare", "distanceToCenterCurves", ArrayReader),
new MKF("EveLensflare", "radialAngleCurves", ArrayReader),
new MKF("EveLensflare", "xDistanceToCenter", ArrayReader),
new MKF("EveLensflare", "bindings", ArrayReader),
new MKF("EveLensflare", "mesh", ObjectReader),
new MKF("EveLensflare", "yDistanceToCenter", ArrayReader),

new MKF("EveLineSet", "effect", ObjectReader),
new MKF("EveLineSet", "name", NameReader),

new MKF("EveLocator2", "name", NameReader),
new MKF("EveLocator2", "transform", Matrix4x4Reader),

new MKF("EveMeshOverlayEffect", "additiveEffects", ArrayReader),
new MKF("EveMeshOverlayEffect", "curveSet", ObjectReader),
new MKF("EveMeshOverlayEffect", "name", NameReader),

new MKF("EveMissile", "boundingSphereCenter", Float3Reader),
new MKF("EveMissile", "boundingSphereRadius", FloatReader),
new MKF("EveMissile", "curveSets", ArrayReader),
new MKF("EveMissile", "name", NameReader),
new MKF("EveMissile", "warheads", ArrayReader),

new MKF("EveMissileWarhead", "acceleration", FloatReader),
new MKF("EveMissileWarhead", "children", ArrayReader),
new MKF("EveMissileWarhead", "durationEjectPhase", FloatReader),
new MKF("EveMissileWarhead", "maxExplosionDistance", FloatReader),
new MKF("EveMissileWarhead", "mesh", ObjectReader),
new MKF("EveMissileWarhead", "startEjectVelocity", FloatReader),

new MKF("EveOccluder", "name", NameReader),
new MKF("EveOccluder", "sprites", ArrayReader),

new MKF("EveParticleDirectForce", "force", Float3Reader),

new MKF("EveParticleDragForce", "drag", FloatReader),

new MKF("EveParticleSpring", "springConstant", FloatReader),

new MKF("EvePlaneSet", "effect", ObjectReader),
new MKF("EvePlaneSet", "hideOnLowQuality", BoolReader),
new MKF("EvePlaneSet", "name", NameReader),
new MKF("EvePlaneSet", "planes", ArrayReader),

new MKF("EvePlaneSetItem", "boneIndex", Int32Reader),
new MKF("EvePlaneSetItem", "color", Float4Reader),
new MKF("EvePlaneSetItem", "layer1Scroll", Float4Reader),
new MKF("EvePlaneSetItem", "layer1Transform", Float4Reader),
new MKF("EvePlaneSetItem", "layer2Scroll", Float4Reader),
new MKF("EvePlaneSetItem", "layer2Transform", Float4Reader),
new MKF("EvePlaneSetItem", "name", NameReader),
new MKF("EvePlaneSetItem", "position", Float3Reader),
new MKF("EvePlaneSetItem", "rotation", Float4Reader),
new MKF("EvePlaneSetItem", "scaling", Float3Reader),

new MKF("EveRootTransform", "boundingSphereRadius", FloatReader),
new MKF("EveRootTransform", "children", ArrayReader),
new MKF("EveRootTransform", "curveSets", ArrayReader),
new MKF("EveRootTransform", "modifier", Int32Reader),
new MKF("EveRootTransform", "name", NameReader),
new MKF("EveRootTransform", "observers", ArrayReader),
new MKF("EveRootTransform", "rotation", Float4Reader),
new MKF("EveRootTransform", "scaling", Float3Reader),
new MKF("EveRootTransform", "translation", Float3Reader),

new MKF("EveShip2", "boosters", ObjectReader),
new MKF("EveShip2", "boundingSphereCenter", Float3Reader),
new MKF("EveShip2", "boundingSphereRadius", FloatReader),
new MKF("EveShip2", "children", ArrayReader),
new MKF("EveShip2", "curveSets", ArrayReader),
new MKF("EveShip2", "damageLocators", damageLocators),
new MKF("EveShip2", "debugShowBoundingBox", BoolReader),
new MKF("EveShip2", "decals", ArrayReader),
new MKF("EveShip2", "highDetailMesh", ObjectReader),
new MKF("EveShip2", "locators", ArrayReader),
new MKF("EveShip2", "lowDetailMesh", ObjectReader),
new MKF("EveShip2", "mediumDetailMesh", ObjectReader),
new MKF("EveShip2", "mesh", ObjectReader),
new MKF("EveShip2", "modelRotationCurve", ObjectReader),
new MKF("EveShip2", "modelTranslationCurve", ObjectReader),
new MKF("EveShip2", "name", NameReader),
new MKF("EveShip2", "observers", ArrayReader),
new MKF("EveShip2", "overlayEffects", ArrayReader),
new MKF("EveShip2", "planeSets", ArrayReader),
new MKF("EveShip2", "rotationCurve", ObjectReader),
new MKF("EveShip2", "shadowEffect", ObjectReader),
new MKF("EveShip2", "spotlightSets", ArrayReader),
new MKF("EveShip2", "spriteSets", ArrayReader),
new MKF("EveShip2", "translationCurve", ObjectReader),
new MKF("EveShip2", "debugRenderDebugInfoForChildren", BoolReader),

new MKF("EveSOFData", "hull", ArrayReader),
new MKF("EveSOFData", "race", ArrayReader),

new MKF("EveSOFDataRace", "name", NameReader),
new MKF("EveSOFDataRace", "booster", ObjectReader),

new MKF("EveSOFDataBooster", "color", Float4Reader),
new MKF("EveSOFDataBooster", "scale", Float4Reader),
new MKF("EveSOFDataBooster", "glowColor", Float4Reader),
new MKF("EveSOFDataBooster", "glowScale", FloatReader),
new MKF("EveSOFDataBooster", "haloColor", Float4Reader),
new MKF("EveSOFDataBooster", "haloScaleX", FloatReader),
new MKF("EveSOFDataBooster", "haloScaleY", FloatReader),
new MKF("EveSOFDataBooster", "symHaloScale", FloatReader),
new MKF("EveSOFDataBooster", "textureResPath", NameReader),
new MKF("EveSOFDataBooster", "trailColor", Float4Reader),
new MKF("EveSOFDataBooster", "trailSize", Float4Reader),
new MKF("EveSOFDataBooster", "soundName", NameReader),

new MKF("EveSOFDataHullBooster", "items", ArrayReader),
new MKF("EveSOFDataHullBooster", "alwaysOn", BoolReader),
new MKF("EveSOFDataHullBooster", "hasTrails", BoolReader),

new MKF("EveSOFDataHullBoosterItem", "hasTrail", BoolReader),
new MKF("EveSOFDataHullBoosterItem", "transform", Matrix4x4Reader),
new MKF("EveSOFDataHullBoosterItem", "functionality", Float4Reader),


// added
new MKF("EveSOFDataHullBoosterItem", "atlasIndex0", Int32Reader),
new MKF("EveSOFDataHullBoosterItem", "atlasIndex1", Int32Reader),
// end

new MKF("EveAnimationStateContainer", "states", ArrayReader),
new MKF("EveAnimationStateContainer", "defaultAnimation", NameReader),

new MKF("EveAnimationState", "name", NameReader),
new MKF("EveAnimationState", "enter", ObjectReader),
new MKF("EveAnimationState", "exit", ObjectReader),
new MKF("EveAnimationState", "main", ObjectReader),
new MKF("EveAnimationState", "transitions", ArrayReader),

new MKF("EveAnimationSequence", "curves", ArrayReader),
new MKF("EveAnimationSequence", "commands", ArrayReader),
new MKF("EveAnimationSequence", "animation", ObjectReader),

new MKF("EveAnimationCurve", "name", NameReader),

new MKF("EveAnimationCommand", "command", ObjectReader),
new MKF("EveAnimationCommand", "data", NameReader),

new MKF("EveAnimation", "name", NameReader),
new MKF("EveAnimation", "loops", Int32Reader),

new MKF("EveTransitionSequence", "transitionState", NameReader),
new MKF("EveTransitionSequence", "animation", ObjectReader),
new MKF("EveTransitionSequence", "curves", ArrayReader),
new MKF("EveTransitionSequence", "commands", ArrayReader),

new MKF("EveSpaceObjectDecal", "decalEffect", ObjectReader),
new MKF("EveSpaceObjectDecal", "name", NameReader),
new MKF("EveSpaceObjectDecal", "parentBoneIndex", Int32Reader),
new MKF("EveSpaceObjectDecal", "position", Float3Reader),
new MKF("EveSpaceObjectDecal", "rotation", Float4Reader),
new MKF("EveSpaceObjectDecal", "scaling", Float3Reader),

new MKF("EveSpaceScene", "ambientColor", Float4Reader),
new MKF("EveSpaceScene", "backgroundEffect", ObjectReader),
new MKF("EveSpaceScene", "backgroundObjects", ArrayReader),
new MKF("EveSpaceScene", "backgroundRenderingEnabled", BoolReader),
new MKF("EveSpaceScene", "curveSets", ArrayReader),
new MKF("EveSpaceScene", "enableShadows", BoolReader),
new MKF("EveSpaceScene", "envMap1ResPath", NameReader),
new MKF("EveSpaceScene", "envMap2ResPath", NameReader),
new MKF("EveSpaceScene", "envMapResPath", NameReader),
new MKF("EveSpaceScene", "envMapRotation", Float4Reader),
new MKF("EveSpaceScene", "fogColor", Float4Reader),
new MKF("EveSpaceScene", "fogEnd", FloatReader),
new MKF("EveSpaceScene", "fogMax", FloatReader),
new MKF("EveSpaceScene", "fogStart", FloatReader),
new MKF("EveSpaceScene", "objects", ArrayReader),
new MKF("EveSpaceScene", "selfShadowOnly", BoolReader),
new MKF("EveSpaceScene", "shadowFadeThreshold", FloatReader),
new MKF("EveSpaceScene", "shadowThreshold", FloatReader),
new MKF("EveSpaceScene", "starfield", ObjectReader),
new MKF("EveSpaceScene", "sunDiffuseColor", Float4Reader),
new MKF("EveSpaceScene", "sunDirection", Float3Reader),

new MKF("EveSpherePin", "centerNormal", Float3Reader),
new MKF("EveSpherePin", "curveSets", ArrayReader),
new MKF("EveSpherePin", "enablePicking", BoolReader),
new MKF("EveSpherePin", "geometryResPath", NameReader),
new MKF("EveSpherePin", "name", NameReader),
new MKF("EveSpherePin", "pinColor", Float4Reader),
new MKF("EveSpherePin", "pinEffect", ObjectReader),
new MKF("EveSpherePin", "pinEffectResPath", NameReader),
new MKF("EveSpherePin", "pinMaxRadius", FloatReader),
new MKF("EveSpherePin", "pinRadius", FloatReader),
new MKF("EveSpherePin", "pinRotation", FloatReader),
new MKF("EveSpherePin", "sortValueMultiplier", FloatReader),

new MKF("EveSpotlightSet", "coneEffect", ObjectReader),
new MKF("EveSpotlightSet", "display", BoolReader),
new MKF("EveSpotlightSet", "glowEffect", ObjectReader),
new MKF("EveSpotlightSet", "name", NameReader),
new MKF("EveSpotlightSet", "spotlightItems", ArrayReader),

new MKF("EveSpotlightSetItem", "boneIndex", Int32Reader),
new MKF("EveSpotlightSetItem", "boosterGainInfluence", BoolReader),
new MKF("EveSpotlightSetItem", "coneColor", Float4Reader),
new MKF("EveSpotlightSetItem", "flareColor", Float4Reader),
new MKF("EveSpotlightSetItem", "name", NameReader),
new MKF("EveSpotlightSetItem", "spriteColor", Float4Reader),
new MKF("EveSpotlightSetItem", "spriteScale", Float3Reader),
new MKF("EveSpotlightSetItem", "transform", Matrix4x4Reader),

new MKF("EveSpriteSet", "display", BoolReader),
new MKF("EveSpriteSet", "effect", ObjectReader),
new MKF("EveSpriteSet", "name", NameReader),
new MKF("EveSpriteSet", "sprites", ArrayReader),

new MKF("EveSpriteSetItem", "blinkPhase", FloatReader),
new MKF("EveSpriteSetItem", "blinkRate", FloatReader),
new MKF("EveSpriteSetItem", "boneIndex", Int32Reader),
new MKF("EveSpriteSetItem", "color", Float4Reader),
new MKF("EveSpriteSetItem", "falloff", FloatReader),
new MKF("EveSpriteSetItem", "maxScale", FloatReader),
new MKF("EveSpriteSetItem", "minScale", FloatReader),
new MKF("EveSpriteSetItem", "name", NameReader),
new MKF("EveSpriteSetItem", "position", Float3Reader),

new MKF("EveStarfield", "effect", ObjectReader),
new MKF("EveStarfield", "maxDist", FloatReader),
new MKF("EveStarfield", "maxFlashRate", FloatReader),
new MKF("EveStarfield", "minDist", FloatReader),
new MKF("EveStarfield", "minFlashIntensity", FloatReader),
new MKF("EveStarfield", "minFlashRate", FloatReader),
new MKF("EveStarfield", "numStars", Int32Reader),
new MKF("EveStarfield", "seed", FloatReader),

new MKF("EveStation2", "boundingSphereCenter", Float3Reader),
new MKF("EveStation2", "boundingSphereRadius", FloatReader),
new MKF("EveStation2", "children", ArrayReader),
new MKF("EveStation2", "curveSets", ArrayReader),
new MKF("EveStation2", "damageLocators", damageLocators),
new MKF("EveStation2", "debugShowBoundingBox", BoolReader),
new MKF("EveStation2", "decals", ArrayReader),
new MKF("EveStation2", "highDetailMesh", ObjectReader),
new MKF("EveStation2", "locators", ArrayReader),
new MKF("EveStation2", "lowDetailMesh", ObjectReader),
new MKF("EveStation2", "mediumDetailMesh", ObjectReader),
new MKF("EveStation2", "mesh", ObjectReader),
new MKF("EveStation2", "modelRotationCurve", ObjectReader),
new MKF("EveStation2", "modelScale", FloatReader),
new MKF("EveStation2", "modelTranslationCurve", ObjectReader),
new MKF("EveStation2", "name", NameReader),
new MKF("EveStation2", "observers", ArrayReader),
new MKF("EveStation2", "planeSets", ArrayReader),
new MKF("EveStation2", "rotationCurve", ObjectReader),
new MKF("EveStation2", "shadowEffect", ObjectReader),
new MKF("EveStation2", "spotlightSets", ArrayReader),
new MKF("EveStation2", "spriteSets", ArrayReader),
new MKF("EveStation2", "translationCurve", ObjectReader),

new MKF("EveStretch", "curveSets", ArrayReader),
new MKF("EveStretch", "dest", ObjectReader),
new MKF("EveStretch", "destObject", ObjectReader),
new MKF("EveStretch", "length", ObjectReader),
new MKF("EveStretch", "name", NameReader),
new MKF("EveStretch", "source", ObjectReader),
new MKF("EveStretch", "sourceObject", ObjectReader),
new MKF("EveStretch", "stretchObject", ObjectReader),

new MKF("EveTrailsSet", "effect", ObjectReader),
new MKF("EveTrailsSet", "geometryResPath", NameReader),

new MKF("EveTransform", "children", ArrayReader),
new MKF("EveTransform", "curveSets", ArrayReader),
new MKF("EveTransform", "debugRenderDebugInfoForChildren", BoolReader),
new MKF("EveTransform", "debugShowBoundingBox", BoolReader),
new MKF("EveTransform", "display", BoolReader),
new MKF("EveTransform", "distanceBasedScaleArg1", FloatReader),
new MKF("EveTransform", "distanceBasedScaleArg2", FloatReader),
new MKF("EveTransform", "hideOnLowQuality", BoolReader),
new MKF("EveTransform", "mesh", ObjectReader),
new MKF("EveTransform", "modifier", Int32Reader),
new MKF("EveTransform", "name", NameReader),
new MKF("EveTransform", "observers", ArrayReader),
new MKF("EveTransform", "particleEmitters", ArrayReader),
new MKF("EveTransform", "particleEmittersGPU", ArrayReader),
new MKF("EveTransform", "particleSystems", ArrayReader),
new MKF("EveTransform", "rotation", Float4Reader),
new MKF("EveTransform", "scaling", Float3Reader),
new MKF("EveTransform", "sortValueMultiplier", FloatReader),
new MKF("EveTransform", "translation", Float3Reader),
new MKF("EveTransform", "update", BoolReader),
new MKF("EveTransform", "useDistanceBasedScale", BoolReader),
new MKF("EveTransform", "useLodLevel", BoolReader),
new MKF("EveTransform", "visibilityThreshold", FloatReader),

new MKF("EveTurretFiringFX", "firingDelay1", FloatReader),
new MKF("EveTurretFiringFX", "firingDelay2", FloatReader),
new MKF("EveTurretFiringFX", "firingDelay3", FloatReader),
new MKF("EveTurretFiringFX", "firingDelay4", FloatReader),
new MKF("EveTurretFiringFX", "isLoopFiring", BoolReader),
new MKF("EveTurretFiringFX", "name", NameReader),
new MKF("EveTurretFiringFX", "stretch", ArrayReader),
new MKF("EveTurretFiringFX", "useMuzzleTransform", BoolReader),

new MKF("EveTurretSet", "alternateFiringAnimCount", Int32Reader),
new MKF("EveTurretSet", "bottomClipHeight", FloatReader),
new MKF("EveTurretSet", "boundingSphere", Float4Reader),
new MKF("EveTurretSet", "firingEffectResPath", NameReader),
new MKF("EveTurretSet", "geometryResPath", NameReader),
new MKF("EveTurretSet", "hasCyclingFiringPos", BoolReader),
new MKF("EveTurretSet", "locatorName", NameReader),
new MKF("EveTurretSet", "name", NameReader),
new MKF("EveTurretSet", "sysBoneHeight", FloatReader),
new MKF("EveTurretSet", "sysBonePitch01Factor", FloatReader),
new MKF("EveTurretSet", "sysBonePitch02Factor", FloatReader),
new MKF("EveTurretSet", "sysBonePitchFactor", FloatReader),
new MKF("EveTurretSet", "sysBonePitchMin", FloatReader),
new MKF("EveTurretSet", "sysBonePitchOffset", FloatReader),
new MKF("EveTurretSet", "trackingFadeTime", ArrayReader),
new MKF("EveTurretSet", "turretEffect", ObjectReader),
new MKF("EveTurretSet", "useRandomFiringDelay", BoolReader),

new MKF("Tr2ClothingActor", "effect", ObjectReader),
new MKF("Tr2ClothingActor", "effectReversed", Int32Reader),
new MKF("Tr2ClothingActor", "morphResEpsilon", FloatReader),
new MKF("Tr2ClothingActor", "resPath", NameReader),

new MKF("Tr2ColorCurve", "cycle", BoolReader),
new MKF("Tr2ColorCurve", "endValue", Float4Reader),
new MKF("Tr2ColorCurve", "keys", ArrayReader),
new MKF("Tr2ColorCurve", "length", FloatReader),
new MKF("Tr2ColorCurve", "name", NameReader),
new MKF("Tr2ColorCurve", "startValue", Float4Reader),

new MKF("Tr2ColorKey", "time", FloatReader),
new MKF("Tr2ColorKey", "value", Float4Reader),

new MKF("Tr2DistanceTracker", "direction", Float3Reader),
new MKF("Tr2DistanceTracker", "name", NameReader),
new MKF("Tr2DistanceTracker", "targetPosition", Float3Reader),

new MKF("Tr2DynamicEmitter", "generators", ArrayReader),
new MKF("Tr2DynamicEmitter", "name", NameReader),
new MKF("Tr2DynamicEmitter", "particleSystem", ObjectReader),
new MKF("Tr2DynamicEmitter", "rate", FloatReader),

new MKF("Tr2Effect", "effectFilePath", NameReader),
new MKF("Tr2Effect", "name", NameReader),
new MKF("Tr2Effect", "parameters", ArrayReader),
new MKF("Tr2Effect", "resources", ArrayReader),
new MKF("Tr2Effect", "useMaxSupportedShaderModel", BoolReader),

new MKF("Tr2ElementBlendConstraint", "elementType", Int32Reader),

new MKF("Tr2EnlightenArea", "albedoColor", Float4Reader),
new MKF("Tr2EnlightenArea", "effect", ObjectReader),
new MKF("Tr2EnlightenArea", "emissiveColor", Float4Reader),
new MKF("Tr2EnlightenArea", "index", Int32Reader),
new MKF("Tr2EnlightenArea", "isEmissive", BoolReader),
new MKF("Tr2EnlightenArea", "name", NameReader),

new MKF("Tr2EulerRotation", "name", NameReader),
new MKF("Tr2EulerRotation", "pitchCurve", ObjectReader),
new MKF("Tr2EulerRotation", "rollCurve", ObjectReader),
new MKF("Tr2EulerRotation", "yawCurve", ObjectReader),

new MKF("Tr2FloatParameter", "allowRerouting", BoolReader),
new MKF("Tr2FloatParameter", "name", NameReader),
new MKF("Tr2FloatParameter", "value", FloatReader),

new MKF("Tr2GPUParticleEmitter", "particleTypes", ArrayReader),
new MKF("Tr2GPUParticleEmitter", "subEmitters", ArrayReader),

new MKF("Tr2GPUParticleSubEmitter", "behaviourName", NameReader),
new MKF("Tr2GPUParticleSubEmitter", "emissionDensity", FloatReader),
new MKF("Tr2GPUParticleSubEmitter", "emissionRate", FloatReader),
new MKF("Tr2GPUParticleSubEmitter", "inheritVelocity", FloatReader),
new MKF("Tr2GPUParticleSubEmitter", "name", NameReader),
new MKF("Tr2GPUParticleSubEmitter", "positionScale", FloatReader),
new MKF("Tr2GPUParticleSubEmitter", "velocityScale", FloatReader),

new MKF("Tr2GPUParticleType", "angularVelocity", FloatReader),
new MKF("Tr2GPUParticleType", "color0", Float4Reader),
new MKF("Tr2GPUParticleType", "color1", Float4Reader),
new MKF("Tr2GPUParticleType", "color2", Float4Reader),
new MKF("Tr2GPUParticleType", "color3", Float4Reader),
new MKF("Tr2GPUParticleType", "dragFactor", FloatReader),
new MKF("Tr2GPUParticleType", "gravityWeight", FloatReader),
new MKF("Tr2GPUParticleType", "lifespan", FloatReader),
new MKF("Tr2GPUParticleType", "lifespanVariance", FloatReader),
new MKF("Tr2GPUParticleType", "name", NameReader),
new MKF("Tr2GPUParticleType", "renderMode", Int32Reader),
new MKF("Tr2GPUParticleType", "size", Float3Reader),
new MKF("Tr2GPUParticleType", "sizeVariance", FloatReader),
new MKF("Tr2GPUParticleType", "texturePath", NameReader),
new MKF("Tr2GPUParticleType", "turbulenceWeight", FloatReader),

new MKF("Tr2GrannyTransformTrack", "grannyResPath", NameReader),
new MKF("Tr2GrannyTransformTrack", "group", NameReader),
new MKF("Tr2GrannyTransformTrack", "name", NameReader),

new MKF("Tr2GrannyVectorTrack", "grannyResPath", NameReader),
new MKF("Tr2GrannyVectorTrack", "group", NameReader),
new MKF("Tr2GrannyVectorTrack", "name", NameReader),

new MKF("Tr2HighLevelShader", "name", NameReader),
new MKF("Tr2HighLevelShader", "parameterDescriptions", ArrayReader),
new MKF("Tr2HighLevelShader", "permuteTags", ArrayReader),
new MKF("Tr2HighLevelShader", "renderClass", NameReader),
new MKF("Tr2HighLevelShader", "shaderPath", NameReader),

new MKF("Tr2InstancedMesh", "additiveAreas", ArrayReader),
new MKF("Tr2InstancedMesh", "decalAreas", ArrayReader),
new MKF("Tr2InstancedMesh", "depthAreas", ArrayReader),
new MKF("Tr2InstancedMesh", "distortionAreas", ArrayReader),
new MKF("Tr2InstancedMesh", "geometryResPath", NameReader),
new MKF("Tr2InstancedMesh", "instanceGeometryResource", ObjectReader),
new MKF("Tr2InstancedMesh", "maxBounds", Float3Reader),
new MKF("Tr2InstancedMesh", "minBounds", Float3Reader),
new MKF("Tr2InstancedMesh", "transparentAreas", ArrayReader),

new MKF("Tr2InteriorCell", "isUnbounded", BoolReader),
new MKF("Tr2InteriorCell", "shProbeResPath", NameReader),
new MKF("Tr2InteriorCell", "systems", ArrayReader),

new MKF("Tr2InteriorEnlightenSystem", "radSystemPath", NameReader),
new MKF("Tr2InteriorEnlightenSystem", "statics", ArrayReader),
new MKF("Tr2InteriorEnlightenSystem", "systemID", Int32Reader),

new MKF("Tr2InteriorFlare", "color", Float4Reader),
new MKF("Tr2InteriorFlare", "flareData", ArrayReader),
new MKF("Tr2InteriorFlare", "flareMaterial", ObjectReader),
new MKF("Tr2InteriorFlare", "name", NameReader),
new MKF("Tr2InteriorFlare", "occluderSize", FloatReader),
new MKF("Tr2InteriorFlare", "transform", Matrix4x4Reader),
new MKF("Tr2InteriorFlare", "transparentFlareData", ArrayReader),
new MKF("Tr2InteriorFlare", "transparentFlareMaterial", ObjectReader),

new MKF("Tr2InteriorFlareData", "centerFadeMaxRadius", FloatReader),
new MKF("Tr2InteriorFlareData", "centerFadeMinRadius", FloatReader),
new MKF("Tr2InteriorFlareData", "directionalStretch", Float2Reader),
new MKF("Tr2InteriorFlareData", "edgeFadeDistance", FloatReader),
new MKF("Tr2InteriorFlareData", "positionWeight", Float2Reader),
new MKF("Tr2InteriorFlareData", "rotation", BoolReader),
new MKF("Tr2InteriorFlareData", "size", Float2Reader),
new MKF("Tr2InteriorFlareData", "textureOffset", Float2Reader),
new MKF("Tr2InteriorFlareData", "textureSize", Float2Reader),

new MKF("Tr2InteriorLightSource", "affectTransparentObjects", BoolReader),
new MKF("Tr2InteriorLightSource", "color", Float4Reader),
new MKF("Tr2InteriorLightSource", "coneAlphaInner", FloatReader),
new MKF("Tr2InteriorLightSource", "coneAlphaOuter", FloatReader),
new MKF("Tr2InteriorLightSource", "coneDirection", Float3Reader),
new MKF("Tr2InteriorLightSource", "falloff", FloatReader),
new MKF("Tr2InteriorLightSource", "importanceBias", FloatReader),
new MKF("Tr2InteriorLightSource", "importanceScale", FloatReader),
new MKF("Tr2InteriorLightSource", "kelvinColor", ObjectReader),
new MKF("Tr2InteriorLightSource", "name", NameReader),
new MKF("Tr2InteriorLightSource", "position", Float3Reader),
new MKF("Tr2InteriorLightSource", "primaryLighting", BoolReader),
new MKF("Tr2InteriorLightSource", "radius", FloatReader),
new MKF("Tr2InteriorLightSource", "secondaryLighting", BoolReader),
new MKF("Tr2InteriorLightSource", "secondaryLightingMultiplier", FloatReader),
new MKF("Tr2InteriorLightSource", "shadowCasterTypes", Int32Reader),
new MKF("Tr2InteriorLightSource", "shadowImportance", FloatReader),
new MKF("Tr2InteriorLightSource", "shadowResolution", Int32Reader),
new MKF("Tr2InteriorLightSource", "specularIntensity", FloatReader),
new MKF("Tr2InteriorLightSource", "useKelvinColor", BoolReader),

new MKF("Tr2InteriorParticleObject", "emitters", ArrayReader),
new MKF("Tr2InteriorParticleObject", "maxParticleRadius", FloatReader),
new MKF("Tr2InteriorParticleObject", "meshes", ArrayReader),
new MKF("Tr2InteriorParticleObject", "name", NameReader),
new MKF("Tr2InteriorParticleObject", "particleSystems", ArrayReader),
new MKF("Tr2InteriorParticleObject", "shBoundsMax", Float3Reader),
new MKF("Tr2InteriorParticleObject", "shBoundsMin", Float3Reader),
new MKF("Tr2InteriorParticleObject", "transform", Matrix4x4Reader),

new MKF("Tr2InteriorPlaceable", "placeableResPath", NameReader),
new MKF("Tr2InteriorPlaceable", "transform", ObjectWithoutIndexReader),

new MKF("Tr2InteriorScene", "cells", ArrayReader),
new MKF("Tr2InteriorScene", "dynamics", ArrayReader),
new MKF("Tr2InteriorScene", "lights", ArrayReader),
new MKF("Tr2InteriorScene", "shScale", FloatReader),

new MKF("Tr2InteriorStatic", "detailMeshes", ArrayReader),
new MKF("Tr2InteriorStatic", "displayTargetMesh", BoolReader),
new MKF("Tr2InteriorStatic", "enlightenAreas", ArrayReader),
new MKF("Tr2InteriorStatic", "geometryResPath", NameReader),
new MKF("Tr2InteriorStatic", "instanceInSystemIdx", Int32Reader),
new MKF("Tr2InteriorStatic", "name", NameReader),
new MKF("Tr2InteriorStatic", "uvLinearTransform", Float4Reader),
new MKF("Tr2InteriorStatic", "uvTranslation", Float2Reader),

new MKF("Tr2IntSkinnedObject", "curveSets", ArrayReader),
new MKF("Tr2IntSkinnedObject", "transform", ObjectWithoutIndexReader),
new MKF("Tr2IntSkinnedObject", "visualModel", ObjectReader),

new MKF("Tr2KelvinColor", "temperature", FloatReader),
new MKF("Tr2KelvinColor", "tint", FloatReader),

new MKF("Tr2MaterialArea", "material", ObjectReader),
new MKF("Tr2MaterialArea", "metatype", NameReader),

new MKF("Tr2MaterialMesh", "areas", DictionaryReader),

new MKF("Tr2MaterialParameterStore", "name", NameReader),
new MKF("Tr2MaterialParameterStore", "parameters", DictionaryReader),

new MKF("Tr2MaterialRes", "meshes", DictionaryReader),

new MKF("Tr2Matrix4Parameter", "name", NameReader),
new MKF("Tr2Matrix4Parameter", "value", Matrix4x4Reader),

new MKF("Tr2MayaScalarCurve", "animationEngine", SkipObjectReader),
new MKF("Tr2MayaScalarCurve", "index", Int32Reader),
new MKF("Tr2MayaScalarCurve", "name", NameReader),

new MKF("Tr2Mesh", "additiveAreas", ArrayReader),
new MKF("Tr2Mesh", "decalAreas", ArrayReader),
new MKF("Tr2Mesh", "decalNormalAreas", ArrayReader),
new MKF("Tr2Mesh", "deferGeometryLoad", BoolReader),
new MKF("Tr2Mesh", "depthAreas", ArrayReader),
new MKF("Tr2Mesh", "depthNormalAreas", ArrayReader),
new MKF("Tr2Mesh", "distortionAreas", ArrayReader),
new MKF("Tr2Mesh", "geometryResPath", NameReader),
new MKF("Tr2Mesh", "isLowDetail", BoolReader),
new MKF("Tr2Mesh", "lowDetailGeometryResPath", NameReader),
new MKF("Tr2Mesh", "meshIndex", Int32Reader),
new MKF("Tr2Mesh", "name", NameReader),
new MKF("Tr2Mesh", "opaqueAreas", ArrayReader),
new MKF("Tr2Mesh", "opaquePrepassAreas", ArrayReader),
new MKF("Tr2Mesh", "pickableAreas", ArrayReader),
new MKF("Tr2Mesh", "transparentAreas", ArrayReader),

new MKF("Tr2MeshArea", "count", Int32Reader),
new MKF("Tr2MeshArea", "effect", ObjectReader),
new MKF("Tr2MeshArea", "index", Int32Reader),
new MKF("Tr2MeshArea", "name", NameReader),
new MKF("Tr2MeshArea", "reversed", BoolReader),
new MKF("Tr2MeshArea", "useSHLighting", BoolReader),

new MKF("Tr2Model", "meshes", ArrayReader),

new MKF("Tr2ParticleAttractorForce", "magnitude", FloatReader),
new MKF("Tr2ParticleAttractorForce", "position", Float3Reader),

new MKF("Tr2ParticleDirectForce", "force", Float3Reader),

new MKF("Tr2ParticleDragForce", "drag", FloatReader),

new MKF("Tr2ParticleElementDeclaration", "customName", NameReader),
new MKF("Tr2ParticleElementDeclaration", "dimension", Int32Reader),
new MKF("Tr2ParticleElementDeclaration", "elementType", Int32Reader),
new MKF("Tr2ParticleElementDeclaration", "usageIndex", Int32Reader),
new MKF("Tr2ParticleElementDeclaration", "usedByGPU", BoolReader),

new MKF("Tr2ParticleFluidDragForce", "drag", FloatReader),

new MKF("Tr2ParticleSpring", "springConstant", FloatReader),

new MKF("Tr2ParticleSystem", "applyAging", BoolReader),
new MKF("Tr2ParticleSystem", "applyForce", BoolReader),
new MKF("Tr2ParticleSystem", "constraints", ArrayReader),
new MKF("Tr2ParticleSystem", "elements", ArrayReader),
new MKF("Tr2ParticleSystem", "emitParticleDuringLifeEmitter", ObjectReader),
new MKF("Tr2ParticleSystem", "emitParticleOnDeathEmitter", ObjectReader),
new MKF("Tr2ParticleSystem", "forces", ArrayReader),
new MKF("Tr2ParticleSystem", "maxParticleCount", Int32Reader),
new MKF("Tr2ParticleSystem", "name", NameReader),
new MKF("Tr2ParticleSystem", "requiresSorting", BoolReader),
new MKF("Tr2ParticleSystem", "updateBoundingBox", BoolReader),
new MKF("Tr2ParticleSystem", "updateSimulation", BoolReader),
new MKF("Tr2ParticleSystem", "useSimTimeRebase", BoolReader),

new MKF("Tr2ParticleTurbulenceForce", "amplitude", Float3Reader),
new MKF("Tr2ParticleTurbulenceForce", "frequency", Float4Reader),
new MKF("Tr2ParticleTurbulenceForce", "noiseLevel", Int32Reader),
new MKF("Tr2ParticleTurbulenceForce", "noiseRatio", FloatReader),

new MKF("Tr2ParticleVortexForce", "magnitude", FloatReader),
new MKF("Tr2ParticleVortexForce", "position", Float3Reader),

new MKF("Tr2PlaneConstraint", "reflectionNoise", FloatReader),

new MKF("Tr2PostProcess", "stages", ArrayReader),

new MKF("Tr2RandomIntegerAttributeGenerator", "customName", NameReader),
new MKF("Tr2RandomIntegerAttributeGenerator", "maxRange", Float4Reader),
new MKF("Tr2RandomUniformAttributeGenerator", "customName", NameReader),
new MKF("Tr2RandomUniformAttributeGenerator", "elementType", Int32Reader),
new MKF("Tr2RandomUniformAttributeGenerator", "maxRange", Float4Reader),
new MKF("Tr2RandomUniformAttributeGenerator", "minRange", Float4Reader),

new MKF("Tr2ScalarCurve", "cycle", BoolReader),
new MKF("Tr2ScalarCurve", "endTangent", FloatReader),
new MKF("Tr2ScalarCurve", "endValue", FloatReader),
new MKF("Tr2ScalarCurve", "interpolation", Int32Reader),
new MKF("Tr2ScalarCurve", "keys", ArrayReader),
new MKF("Tr2ScalarCurve", "length", FloatReader),
new MKF("Tr2ScalarCurve", "name", NameReader),
new MKF("Tr2ScalarCurve", "reversed", BoolReader),
new MKF("Tr2ScalarCurve", "startTangent", FloatReader),
new MKF("Tr2ScalarCurve", "startValue", FloatReader),
new MKF("Tr2ScalarCurve", "timeScale", FloatReader),
new MKF("Tr2ScalarCurve", "timeOffset", FloatReader),

new MKF("Tr2ScalarExprCurve", "cycle", BoolReader),
new MKF("Tr2ScalarExprCurve", "endValue", FloatReader),
new MKF("Tr2ScalarExprCurve", "expr", NameReader),
new MKF("Tr2ScalarExprCurve", "input1", FloatReader),
new MKF("Tr2ScalarExprCurve", "input2", FloatReader),
new MKF("Tr2ScalarExprCurve", "input3", FloatReader),
new MKF("Tr2ScalarExprCurve", "input4", FloatReader),
new MKF("Tr2ScalarExprCurve", "length", FloatReader),
new MKF("Tr2ScalarExprCurve", "name", NameReader),
new MKF("Tr2ScalarExprCurve", "randomMax", FloatReader),
new MKF("Tr2ScalarExprCurve", "randomMin", FloatReader),
new MKF("Tr2ScalarExprCurve", "sourcePositionB", Float3Reader),
new MKF("Tr2ScalarExprCurve", "startValue", FloatReader),
new MKF("Tr2ScalarExprCurve", "updateDistance", BoolReader),

new MKF("Tr2ScalarExprKey", "input1", FloatReader),
new MKF("Tr2ScalarExprKey", "input2", FloatReader),
new MKF("Tr2ScalarExprKey", "input3", FloatReader),
new MKF("Tr2ScalarExprKey", "interpolation", Int32Reader),
new MKF("Tr2ScalarExprKey", "left", FloatReader),
new MKF("Tr2ScalarExprKey", "randomMax", FloatReader),
new MKF("Tr2ScalarExprKey", "randomMin", FloatReader),
new MKF("Tr2ScalarExprKey", "right", FloatReader),
new MKF("Tr2ScalarExprKey", "time", FloatReader),
new MKF("Tr2ScalarExprKey", "timeExpression", NameReader),
new MKF("Tr2ScalarExprKey", "value", FloatReader),
new MKF("Tr2ScalarExprKey", "valueExpression", NameReader),

new MKF("Tr2ScalarExprKeyCurve", "interpolation", Int32Reader),
new MKF("Tr2ScalarExprKeyCurve", "keys", ArrayReader),
new MKF("Tr2ScalarExprKeyCurve", "name", NameReader),

new MKF("Tr2ScalarKey", "interpolation", Int32Reader),
new MKF("Tr2ScalarKey", "leftTangent", FloatReader),
new MKF("Tr2ScalarKey", "rightTangent", FloatReader),
new MKF("Tr2ScalarKey", "time", FloatReader),
new MKF("Tr2ScalarKey", "value", FloatReader),

new MKF("Tr2ShaderFloat2Desc", "defaultValue", Float2Reader),
new MKF("Tr2ShaderFloat2Desc", "helpText", NameReader),
new MKF("Tr2ShaderFloat2Desc", "name", NameReader),
new MKF("Tr2ShaderFloat2Desc", "parameterName", NameReader),
new MKF("Tr2ShaderFloat2Desc", "section", NameReader),

new MKF("Tr2ShaderFloat3Desc", "defaultValue", Float3Reader),
new MKF("Tr2ShaderFloat3Desc", "helpText", NameReader),
new MKF("Tr2ShaderFloat3Desc", "isColor", BoolReader),
new MKF("Tr2ShaderFloat3Desc", "name", NameReader),
new MKF("Tr2ShaderFloat3Desc", "parameterName", NameReader),
new MKF("Tr2ShaderFloat3Desc", "section", NameReader),

new MKF("Tr2ShaderFloat4Desc", "defaultValue", Float4Reader),
new MKF("Tr2ShaderFloat4Desc", "helpText", NameReader),
new MKF("Tr2ShaderFloat4Desc", "isColor", BoolReader),
new MKF("Tr2ShaderFloat4Desc", "name", NameReader),
new MKF("Tr2ShaderFloat4Desc", "parameterName", NameReader),
new MKF("Tr2ShaderFloat4Desc", "section", NameReader),

new MKF("Tr2ShaderFloatDesc", "defaultValue", FloatReader),
new MKF("Tr2ShaderFloatDesc", "helpText", NameReader),
new MKF("Tr2ShaderFloatDesc", "name", NameReader),
new MKF("Tr2ShaderFloatDesc", "parameterName", NameReader),
new MKF("Tr2ShaderFloatDesc", "section", NameReader),

new MKF("Tr2ShaderMaterial", "defaultSituation", NameReader),
new MKF("Tr2ShaderMaterial", "highLevelShaderName", NameReader),
new MKF("Tr2ShaderMaterial", "name", NameReader),
new MKF("Tr2ShaderMaterial", "parameters", DictionaryReader),

new MKF("Tr2ShaderPermuteTag", "name", NameReader),
new MKF("Tr2ShaderPermuteTag", "permuteDefineString", NameReader),
new MKF("Tr2ShaderPermuteTag", "precompileHint", Int32Reader),
new MKF("Tr2ShaderPermuteTag", "predicate", NameReader),
new MKF("Tr2ShaderPermuteTag", "tagBits", Int32Reader),
new MKF("Tr2ShaderPermuteTag", "unused", NameReader),

new MKF("Tr2ShaderTexDesc", "defaultTexture", NameReader),
new MKF("Tr2ShaderTexDesc", "helpText", NameReader),
new MKF("Tr2ShaderTexDesc", "name", NameReader),
new MKF("Tr2ShaderTexDesc", "parameterName", NameReader),
new MKF("Tr2ShaderTexDesc", "section", NameReader),

new MKF("Tr2SkinnedModel", "geometryResPath", NameReader),
new MKF("Tr2SkinnedModel", "meshes", ArrayReader),
new MKF("Tr2SkinnedModel", "name", NameReader),
new MKF("Tr2SkinnedModel", "skeletonName", NameReader),

new MKF("Tr2SphereShapeAttributeGenerator", "maxPhi", FloatReader),
new MKF("Tr2SphereShapeAttributeGenerator", "maxRadius", FloatReader),
new MKF("Tr2SphereShapeAttributeGenerator", "maxSpeed", FloatReader),
new MKF("Tr2SphereShapeAttributeGenerator", "maxTheta", FloatReader),
new MKF("Tr2SphereShapeAttributeGenerator", "minPhi", FloatReader),
new MKF("Tr2SphereShapeAttributeGenerator", "minRadius", FloatReader),
new MKF("Tr2SphereShapeAttributeGenerator", "minSpeed", FloatReader),
new MKF("Tr2SphereShapeAttributeGenerator", "minTheta", FloatReader),
new MKF("Tr2SphereShapeAttributeGenerator", "parentVelocityFactor", FloatReader),
new MKF("Tr2SphereShapeAttributeGenerator", "position", Float3Reader),
new MKF("Tr2SphereShapeAttributeGenerator", "rotation", Float4Reader),

new MKF("Tr2StaticEmitter", "geometryResourcePath", NameReader),
new MKF("Tr2StaticEmitter", "meshIndex", Int32Reader),
new MKF("Tr2StaticEmitter", "name", NameReader),
new MKF("Tr2StaticEmitter", "particleSystem", ObjectReader),

new MKF("Tr2Vector2Parameter", "name", NameReader),
new MKF("Tr2Vector2Parameter", "value", Float2Reader),

new MKF("Tr2Vector3Curve", "timeOffset", FloatReader),
new MKF("Tr2Vector3Curve", "cycle", BoolReader),
new MKF("Tr2Vector3Curve", "endTangent", Float3Reader),
new MKF("Tr2Vector3Curve", "endValue", Float3Reader),
new MKF("Tr2Vector3Curve", "interpolation", Int32Reader),
new MKF("Tr2Vector3Curve", "keys", ArrayReader),
new MKF("Tr2Vector3Curve", "length", FloatReader),
new MKF("Tr2Vector3Curve", "name", NameReader),
new MKF("Tr2Vector3Curve", "startTangent", Float3Reader),
new MKF("Tr2Vector3Curve", "startValue", Float3Reader),

new MKF("Tr2Vector3Key", "interpolation", Int32Reader),
new MKF("Tr2Vector3Key", "leftTangent", Float3Reader),
new MKF("Tr2Vector3Key", "rightTangent", Float3Reader),
new MKF("Tr2Vector3Key", "time", FloatReader),
new MKF("Tr2Vector3Key", "value", Float3Reader),

new MKF("Tr2Vector3Parameter", "name", NameReader),
new MKF("Tr2Vector3Parameter", "value", Float3Reader),

new MKF("Tr2Vector4Parameter", "name", NameReader),
new MKF("Tr2Vector4Parameter", "value", Float4Reader),

new MKF("TriColorCurve", "extrapolation", Int32Reader),
new MKF("TriColorCurve", "keys", ArrayReader),
new MKF("TriColorCurve", "length", FloatReader),
new MKF("TriColorCurve", "name", NameReader),
new MKF("TriColorCurve", "start", Int64Reader),
new MKF("TriColorCurve", "useHSV", BoolReader),
new MKF("TriColorCurve", "value", Float4Reader),

new MKF("TriColorKey", "interpolation", Int32Reader),
new MKF("TriColorKey", "left", Float4Reader),
new MKF("TriColorKey", "right", Float4Reader),
new MKF("TriColorKey", "time", FloatReader),
new MKF("TriColorKey", "value", Float4Reader),

new MKF("TriColorSequencer", "functions", ArrayReader),
new MKF("TriColorSequencer", "name", NameReader),
new MKF("TriColorSequencer", "operator_", Int32Reader),
new MKF("TriColorSequencer", "value", Float4Reader),

new MKF("TriCurveSet", "bindings", ArrayReader),
new MKF("TriCurveSet", "curves", ArrayReader),
new MKF("TriCurveSet", "name", NameReader),
new MKF("TriCurveSet", "playOnLoad", BoolReader),
new MKF("TriCurveSet", "scale", FloatReader),
new MKF("TriCurveSet", "useSimTimeRebase", BoolReader),

new MKF("TriEventCurve", "extrapolation", Int32Reader),
new MKF("TriEventCurve", "keys", ArrayReader),
new MKF("TriEventCurve", "name", NameReader),
new MKF("TriEventCurve", "value", NameReader),

new MKF("TriEventKey", "time", FloatReader),
new MKF("TriEventKey", "value", NameReader),

new MKF("TriFloat", "value", FloatReader),

new MKF("TriFloatParameter", "name", NameReader),
new MKF("TriFloatParameter", "value", FloatReader),

new MKF("TriMatrix", "_11", FloatReader),
new MKF("TriMatrix", "_22", FloatReader),
new MKF("TriMatrix", "_23", FloatReader),
new MKF("TriMatrix", "_32", FloatReader),
new MKF("TriMatrix", "_33", FloatReader),
new MKF("TriMatrix", "_42", FloatReader),
new MKF("TriMatrix", "_43", FloatReader),

new MKF("TriObserverLocal", "front", Float3Reader),
new MKF("TriObserverLocal", "observer", ObjectReader),

new MKF("TriPerlinCurve", "alpha", FloatReader),
new MKF("TriPerlinCurve", "beta", FloatReader),
new MKF("TriPerlinCurve", "N", Int32Reader),
new MKF("TriPerlinCurve", "name", NameReader),
new MKF("TriPerlinCurve", "offset", FloatReader),
new MKF("TriPerlinCurve", "scale", FloatReader),
new MKF("TriPerlinCurve", "speed", FloatReader),
new MKF("TriPerlinCurve", "value", FloatReader),

new MKF("TriQuaternionKey", "interpolation", Int32Reader),
new MKF("TriQuaternionKey", "left", Float4Reader),
new MKF("TriQuaternionKey", "right", Float4Reader),
new MKF("TriQuaternionKey", "time", FloatReader),
new MKF("TriQuaternionKey", "value", Float4Reader),

new MKF("TriQuaternionSequencer", "functions", ArrayReader),
new MKF("TriQuaternionSequencer", "name", NameReader),
new MKF("TriQuaternionSequencer", "value", Float4Reader),

new MKF("TriRandomConstantCurve", "name", NameReader),

new MKF("TriRGBAScalarSequencer", "AlphaCurve", ObjectReader),
new MKF("TriRGBAScalarSequencer", "BlueCurve", ObjectReader),
new MKF("TriRGBAScalarSequencer", "GreenCurve", ObjectReader),
new MKF("TriRGBAScalarSequencer", "RedCurve", ObjectReader),
new MKF("TriRGBAScalarSequencer", "value", Float4Reader),

new MKF("TriRigidOrientation", "drag", FloatReader),
new MKF("TriRigidOrientation", "I", FloatReader),
new MKF("TriRigidOrientation", "value", Float4Reader),

new MKF("TriRotationCurve", "extrapolation", Int32Reader),
new MKF("TriRotationCurve", "keys", ArrayReader),
new MKF("TriRotationCurve", "length", FloatReader),
new MKF("TriRotationCurve", "name", NameReader),
new MKF("TriRotationCurve", "start", Int64Reader),
new MKF("TriRotationCurve", "value", Float4Reader),

new MKF("TriScalarCurve", "extrapolation", Int32Reader),
new MKF("TriScalarCurve", "keys", ArrayReader),
new MKF("TriScalarCurve", "length", FloatReader),
new MKF("TriScalarCurve", "name", NameReader),
new MKF("TriScalarCurve", "start", Int64Reader),
new MKF("TriScalarCurve", "timeScale", FloatReader),
new MKF("TriScalarCurve", "value", FloatReader),

new MKF("TriScalarKey", "interpolation", Int32Reader),
new MKF("TriScalarKey", "left", FloatReader),
new MKF("TriScalarKey", "right", FloatReader),
new MKF("TriScalarKey", "time", FloatReader),
new MKF("TriScalarKey", "value", FloatReader),

new MKF("TriScalarSequencer", "clamping", BoolReader),
new MKF("TriScalarSequencer", "functions", ArrayReader),
new MKF("TriScalarSequencer", "inMaxClamp", FloatReader),
new MKF("TriScalarSequencer", "inMinClamp", FloatReader),
new MKF("TriScalarSequencer", "name", NameReader),
new MKF("TriScalarSequencer", "outMaxClamp", FloatReader),
new MKF("TriScalarSequencer", "outMinClamp", FloatReader),
new MKF("TriScalarSequencer", "value", FloatReader),

new MKF("TriSineCurve", "name", NameReader),
new MKF("TriSineCurve", "offset", FloatReader),
new MKF("TriSineCurve", "scale", FloatReader),
new MKF("TriSineCurve", "speed", FloatReader),
new MKF("TriSineCurve", "value", FloatReader),

new MKF("TriTexture2DParameter", "addressUMode", Int32Reader),
new MKF("TriTexture2DParameter", "addressVMode", Int32Reader),
new MKF("TriTexture2DParameter", "addressWMode", Int32Reader),
new MKF("TriTexture2DParameter", "filterMode", Int32Reader),
new MKF("TriTexture2DParameter", "maxAnisotropy", Int32Reader),
new MKF("TriTexture2DParameter", "maxMipLevel", Int32Reader),
new MKF("TriTexture2DParameter", "mipFilterMode", Int32Reader),
new MKF("TriTexture2DParameter", "mipmapLodBias", FloatReader),
new MKF("TriTexture2DParameter", "name", NameReader),
new MKF("TriTexture2DParameter", "resourcePath", NameReader),
new MKF("TriTexture2DParameter", "Srgb", BoolReader),
new MKF("TriTexture2DParameter", "useAllOverrides", BoolReader),

new MKF("TriTextureCubeParameter", "name", NameReader),
new MKF("TriTextureCubeParameter", "resourcePath", NameReader),

new MKF("TriTransformParameter", "name", NameReader),
new MKF("TriTransformParameter", "rotation", Float4Reader),
new MKF("TriTransformParameter", "rotationCenter", Float3Reader),
new MKF("TriTransformParameter", "scaling", Float3Reader),
new MKF("TriTransformParameter", "transformBase", Int32Reader),
new MKF("TriTransformParameter", "translation", Float3Reader),
new MKF("TriTransformParameter", "worldTransform", Matrix4x4Reader),

new MKF("TriValueBinding", "destinationAttribute", NameReader),
new MKF("TriValueBinding", "destinationObject", ObjectReader),
new MKF("TriValueBinding", "name", NameReader),
new MKF("TriValueBinding", "offset", Float4Reader),
new MKF("TriValueBinding", "scale", FloatReader),
new MKF("TriValueBinding", "sourceAttribute", NameReader),
new MKF("TriValueBinding", "sourceObject", ObjectReader),

new MKF("TriVariableParameter", "name", NameReader),
new MKF("TriVariableParameter", "variableName", NameReader),

new MKF("TriVector4Parameter", "name", NameReader),
new MKF("TriVector4Parameter", "v1", FloatReader),
new MKF("TriVector4Parameter", "v2", FloatReader),
new MKF("TriVector4Parameter", "v3", FloatReader),
new MKF("TriVector4Parameter", "v4", FloatReader),

new MKF("TriVectorCurve", "extrapolation", Int32Reader),
new MKF("TriVectorCurve", "keys", ArrayReader),
new MKF("TriVectorCurve", "length", FloatReader),
new MKF("TriVectorCurve", "name", NameReader),
new MKF("TriVectorCurve", "start", Int64Reader),
new MKF("TriVectorCurve", "value", Float3Reader),

new MKF("TriVectorKey", "interpolation", Int32Reader),
new MKF("TriVectorKey", "left", Float3Reader),
new MKF("TriVectorKey", "right", Float3Reader),
new MKF("TriVectorKey", "time", FloatReader),
new MKF("TriVectorKey", "value", Float3Reader),

new MKF("TriVectorSequencer", "functions", ArrayReader),
new MKF("TriVectorSequencer", "value", Float3Reader),

new MKF("TriXYZScalarSequencer", "name", NameReader),
new MKF("TriXYZScalarSequencer", "value", Float3Reader),
new MKF("TriXYZScalarSequencer", "XCurve", ObjectReader),
new MKF("TriXYZScalarSequencer", "YCurve", ObjectReader),
new MKF("TriXYZScalarSequencer", "ZCurve", ObjectReader),

new MKF("TriYPRSequencer", "name", NameReader),
new MKF("TriYPRSequencer", "PitchCurve", ObjectReader),
new MKF("TriYPRSequencer", "RollCurve", ObjectReader),
new MKF("TriYPRSequencer", "value", Float4Reader),
new MKF("TriYPRSequencer", "YawCurve", ObjectReader),
new MKF("TriYPRSequencer", "YawPitchRoll", Float3Reader),

new MKF("WodPlaceableRes", "farFadeDistance", FloatReader),
new MKF("WodPlaceableRes", "nearFadeDistance", FloatReader),
new MKF("WodPlaceableRes", "visualModel", ObjectReader),

new MKF("EveMobile", "activationStrength", FloatReader),
new MKF("EveMobile", "boundingSphereCenter", Float3Reader),
new MKF("EveMobile", "boundingSphereRadius", FloatReader),
new MKF("EveMobile", "children", ArrayReader),
new MKF("EveMobile", "curveSets", ArrayReader),
new MKF("EveMobile", "damageLocators", damageLocators),
new MKF("EveMobile", "debugShowBoundingBox", BoolReader),
new MKF("EveMobile", "decals", ArrayReader),
new MKF("EveMobile", "highDetailMesh", ObjectReader),
new MKF("EveMobile", "locators", ArrayReader),
new MKF("EveMobile", "lowDetailMesh", ObjectReader),
new MKF("EveMobile", "mediumDetailMesh", ObjectReader),
new MKF("EveMobile", "mesh", ObjectReader),
new MKF("EveMobile", "modelRotationCurve", ObjectReader),
new MKF("EveMobile", "modelScale", FloatReader),
new MKF("EveMobile", "modelTranslationCurve", ObjectReader),
new MKF("EveMobile", "name", NameReader),
new MKF("EveMobile", "observers", ArrayReader),
new MKF("EveMobile", "planeSets", ArrayReader),
new MKF("EveMobile", "rotationCurve", ObjectReader),
new MKF("EveMobile", "shadowEffect", ObjectReader),
new MKF("EveMobile", "spotlightSets", ArrayReader),
new MKF("EveMobile", "spriteSets", ArrayReader),
new MKF("EveMobile", "translationCurve", ObjectReader),


new MKF("Tr2MayaEulerRotationCurve", "yIndex", Int32Reader),
new MKF("Tr2MayaEulerRotationCurve", "animationEngine", SkipObjectReader),
new MKF("Tr2MayaEulerRotationCurve", "name", NameReader),
new MKF("Tr2MayaEulerRotationCurve", "updateQuaternion", BoolReader),

new MKF("Tr2MayaAnimationEngine", "curves", Int32Reader),


 }.ToDictionary(x => x.Item1);

        Dictionary<string, object> dictionary = new Dictionary<string, object>();


        public int Count { get { return dictionary.Count; } }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return dictionary.TryGetValue(binder.Name, out result);
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            dictionary[binder.Name] = value;
            return true;
        }

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return dictionary.Keys;
        }

        public override string ToString()
        {
            return this.Type;
        }
    }
}
