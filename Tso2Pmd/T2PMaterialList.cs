using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

using TDCG;
using TDCGUtils;

namespace Tso2Pmd
{
    class T2PMaterialList
    {
        public List<PMD_Material> materials = new List<PMD_Material>();

        List<TSOFile> tsos;
        List<string> categories;
        T2PTextureList tex_list;

        EdgeInform edgelist;

        public T2PMaterialList(List<TSOFile> tsos, List<string> categories, string file_prefix, bool use_spheremap)
        {
            this.tsos = tsos;
            this.categories = categories;
            edgelist = new EdgeInform();
            // テクスチャを準備
            tex_list = new T2PTextureList(file_prefix, use_spheremap);
            int tso_num = 0;
            foreach (TSOFile tso in tsos)
            {
                foreach (TSOTex tex in tso.textures)
                {
                    tex_list.Add(tex, tso_num);
                }
                tso_num++;
            }
        }

        public void Save(string dest_path, string file_name)
        {
            tex_list.Save(dest_path);
            SaveNamesToFile(dest_path + "/" + file_name + ".txt");
        }

        /// <summary>
        /// マテリアル名のリストを保存します。
        /// </summary>
        /// <param name="dest_file">保存ファイル名</param>
        void SaveNamesToFile(string dest_file)
        {
            using (StreamWriter sw = new StreamWriter(dest_file, false,
                System.Text.Encoding.GetEncoding("shift_jis")))
            {
                foreach (PMD_Material material in materials)
                    sw.WriteLine(material.name);
            }
        }
       
        // TSOSubScriptを元にPMD_Materialを生成して追加する
        // 頂点インデックス数は後で設定する
        public void Add(int tso_num, int script_num, bool use_edge)
        {
            Add(tso_num, script_num, use_edge, "memo");
        }

        // TSOSubScriptを元にPMD_Materialを生成して追加する
        // 頂点インデックス数は後で設定する
        public void Add(int tso_num, int script_num, bool use_edge, string memo)
        {
            PMD_Material pmd_m = new PMD_Material();

            pmd_m.name = categories[tso_num] + " " + tsos[tso_num].sub_scripts[script_num].Name;

            // スクリプトよりシェーダパラメータを取得
            Shader shader = new Shader();
            shader.Load(tsos[tso_num].sub_scripts[script_num].lines);

            pmd_m.diffuse = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            pmd_m.specular = new Vector4(0.15f, 0.15f, 0.15f, 6.0f);
            pmd_m.ambient = new Vector3(0.5f, 0.5f, 0.5f);
/*
            pmd_m.UseEdge = use_edge;
            pmd_m.UseShadow = use_edge;
*/
            foreach (ShaderParameter pm in shader.shader_parameters)
            {
                // エッジ色があれば、materialに設定する. 
                if (pm.Name == "PenColor")
                    pmd_m.edge_color = pm.GetFloat4();
                // 輪郭線の太さを設定する.
                if (pm.Name == "Thickness")
                {
                    pmd_m.edge_width = pm.GetFloat() * 100F;
                    // MMDとカス子で挙動が違うので補正する
                    if (pmd_m.edge_width < 0F) pmd_m.edge_width = 0F;
                    if (pmd_m.edge_width > 1F) pmd_m.edge_width = 1F;
                }
                // エッジが出るtechniqueのみエッジをつける.
                if(use_edge)
                    if (pm.Name == "technique")
                    {
                        // dictionaryを見て該当するならエッジをつける
                        if (edgelist.hasedge(pm.GetValueString()))
                            pmd_m.UseEdge = use_edge;
                        // 影は常に落とす
                        pmd_m.UseShadow = use_edge;
                    }
            }

            pmd_m.memo += " " + memo;

            // 頂点インデックス数（後で設定する）
            pmd_m.vindices_count = 0;

            // colorテクスチャ
            pmd_m.tex_id = tex_list.GetBitmapID(tso_num, shader.ColorTexName);

            // toonテクスチャ
            pmd_m.tex_toon_id = tex_list.GetBitmapID(tso_num, shader.ShadeTexName);

            // スフィアマップを使う
            if (tex_list.use_spheremap)
            {
                pmd_m.tex_sphere_id = tex_list.GetSphereID(tso_num, shader.ShadeTexName);

                if (pmd_m.tex_sphere_id != -1)
                    pmd_m.sphere_mode = 1;
            }

            // 要素を追加
            materials.Add(pmd_m);
        }

        // 隣り合う同一のマテリアルを統合する
        public void UniqueMaterials()
        {
            for (int i = 0; i < materials.Count - 1; i++)
            {
                if (EqualMaterials(materials[i], materials[i + 1]))
                {
                    materials[i].vindices_count += materials[i + 1].vindices_count;
                    materials.RemoveAt(i + 1);
                    i = 0;
                }
            }
        }

        // ２つのマテリアルが等しいか判定する
        public bool EqualMaterials(PMD_Material m1, PMD_Material m2)
        {
            if (m1.flags != m2.flags)
                return false;

            if (m1.tex_id != m2.tex_id)
                return false;
            
            if (m1.tex_toon_id != m2.tex_toon_id)
                return false;

            if (m1.edge_color != m2.edge_color)
                return false;

            if (m1.edge_width != m2.edge_width)
                return false;

            return true;
        }

        public string[] GetTextureFileNameList()
        {
            return tex_list.GetFileNameList();
        }
    }
}
