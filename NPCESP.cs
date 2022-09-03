using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NPCFinder;

public static class Npcesp
{
    private static readonly List<Piece> SNpcPieces = new();
    private static Texture2D sNpcMarketplaceTexture;

    private static float _sUpdateTimer = 0f;
    private const float SUpdateTimerInterval = 1.5f;

    static Npcesp()
    {
        sNpcMarketplaceTexture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
        sNpcMarketplaceTexture.SetPixel(0, 0, Color.yellow);
        sNpcMarketplaceTexture.SetPixel(1, 0, Color.yellow);
        sNpcMarketplaceTexture.SetPixel(0, 1, Color.yellow);
        sNpcMarketplaceTexture.SetPixel(1, 1, Color.yellow);
        sNpcMarketplaceTexture.Apply();
    }

    public static void Update()
    {
        if (!(Time.time >= _sUpdateTimer)) return;
        SNpcPieces.Clear();

        if (!NpcFinderPlugin.SShowNpcesp) return;
        Piece[]? npcPieces = Resources.FindObjectsOfTypeAll<Piece>();

        if (npcPieces == null || Camera.main == null || Player.m_localPlayer == null) return;
        foreach (Piece npcPiece in npcPieces)
        {
            if (npcPiece.gameObject.name.Contains("MarketPlaceNPC") || npcPiece.gameObject.name.ToLower().Contains("npc"))
            {
                float distance = Vector3.Distance(Camera.main.transform.position,
                    npcPiece.transform.position);

                if (distance > 2)
                {
                    SNpcPieces.Add(npcPiece);
                }
            }

            _sUpdateTimer = Time.time + SUpdateTimerInterval;
        }
    }

    public static void DisplayGUI()
    {
        if (Camera.main == null || Player.m_localPlayer == null) return;
        Camera main = Camera.main;
        GUIStyle labelSkin = new(GUI.skin.label);

        if (!NpcFinderPlugin.SShowNpcesp) return;
        labelSkin.normal.textColor = Color.yellow;
        foreach (Piece npcPiece in SNpcPieces)
        {
            if (npcPiece == null)
            {
                continue;
            }

            Vector3 vector = main.WorldToScreenPoint(npcPiece.transform.position);

            if (!(vector.z > -1)) continue;
            int distance = (int)Vector3.Distance(main.transform.position, npcPiece.transform.position);
            string espLabel = "";
            //float a = Math.Abs(main.WorldToScreenPoint(npcPiece.m_localCenter).y - vector.y);
            GameObject npc = npcPiece.gameObject;
            try
            {
                espLabel =
                    $"{npc.GetComponentInChildren<Canvas>().gameObject.transform.Find("Text").gameObject.GetComponent<Text>().text} [{distance}]";
            }
            catch
            {
                espLabel =
                    $"{npc.name} [{distance}]";
            }

            //Box(vector.x, Screen.height - vector.y, a * 0.65f, a, sNpcMarketplaceTexture, 1f);
            GUI.Label(new Rect((int)vector.x - 5, Screen.height - vector.y - 5, 150, 40), espLabel,
                labelSkin);
        }
    }

    private static void Box(float x, float y, float width, float height, Texture2D text, float thickness = 1f)
    {
        RectOutlined(x - width / 2f, y - height, width, height, text, thickness);
    }

    private static void RectOutlined(float x, float y, float width, float height, Texture2D text, float thickness = 1f)
    {
        RectFilled(x, y, thickness, height, text);
        RectFilled(x + width - thickness, y, thickness, height, text);
        RectFilled(x + thickness, y, width - thickness * 2f, thickness, text);
        RectFilled(x + thickness, y + height - thickness, width - thickness * 2f, thickness, text);
    }

    private static void RectFilled(float x, float y, float width, float height, Texture2D text)
    {
        GUI.DrawTexture(new Rect(x, y, width, height), text);
    }
}