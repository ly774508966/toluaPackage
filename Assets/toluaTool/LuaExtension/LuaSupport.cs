﻿/*
Copyright (c) 2015-2016 topameng(topameng@qq.com)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
using UnityEngine;
using System.Collections.Generic;
using LuaInterface;
using System.Collections;
using System.IO;
using System;
using UnityEngine.SceneManagement;

public class LuaSupport : MonoBehaviour
{
    public static LuaSupport Instance
    {
        get;
        protected set;
    }

    protected static LuaState luaState = null;
    protected LuaLooper loop = null;
    protected LuaFunction levelLoaded = null;

    protected bool openLuaSocket = false;
    protected bool beZbStart = false;

    public string mainClass = "LuaEntrance";

    protected virtual LuaFileUtils InitLoader()
    {
        if (LuaFileUtils.Instance != null)
        {
            return LuaFileUtils.Instance;
        }

        return new LuaFileUtils();
    }

    protected virtual void LoadLuaFiles()
    {
        OnLoadFinished();
    }

    protected virtual void OpenLibs()
    {
        luaState.OpenLibs(LuaDLL.luaopen_pb);
        luaState.OpenLibs(LuaDLL.luaopen_struct);
        luaState.OpenLibs(LuaDLL.luaopen_lpeg);
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        luaState.OpenLibs(LuaDLL.luaopen_bit);
#endif

        if (LuaConst.openLuaSocket)
        {
            OpenLuaSocket();
        }

        if (LuaConst.openZbsDebugger)
        {
            OpenZbsDebugger();
        }

        OpenCJson();
    }

    public void OpenZbsDebugger(string ip = "localhost")
    {
        if (!Directory.Exists(LuaConst.zbsDir))
        {
            Debugger.LogWarning("ZeroBraneStudio not install or LuaConst.zbsDir not right");
            return;
        }

        if (!LuaConst.openLuaSocket)
        {
            OpenLuaSocket();
        }

        if (!string.IsNullOrEmpty(LuaConst.zbsDir))
        {
            luaState.AddSearchPath(LuaConst.zbsDir);
        }

        luaState.LuaDoString(string.Format("DebugServerIp = '{0}'", ip));
    }

    [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
    static int LuaOpen_Socket_Core(IntPtr L)
    {
        return LuaDLL.luaopen_socket_core(L);
    }

    [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
    static int LuaOpen_Mime_Core(IntPtr L)
    {
        return LuaDLL.luaopen_mime_core(L);
    }

    protected void OpenLuaSocket()
    {
        LuaConst.openLuaSocket = true;

        luaState.BeginPreLoad();
        luaState.RegFunction("socket.core", LuaOpen_Socket_Core);
        luaState.RegFunction("mime.core", LuaOpen_Mime_Core);
        luaState.EndPreLoad();
    }

    //cjson 比较特殊，只new了一个table，没有注册库，这里注册一下
    protected void OpenCJson()
    {
        luaState.LuaGetField(LuaIndexes.LUA_REGISTRYINDEX, "_LOADED");
        luaState.OpenLibs(LuaDLL.luaopen_cjson);
        luaState.LuaSetField(-2, "cjson");

        luaState.OpenLibs(LuaDLL.luaopen_cjson_safe);
        luaState.LuaSetField(-2, "cjson.safe");
    }

    protected virtual void CallMain()
    {
        LuaFunction main = luaState.GetFunction("Main");
        main.Call();
        main.Dispose();
        main = null;
    }

    protected virtual void StartMain()
    {
        luaState.DoFile(mainClass);
        levelLoaded = luaState.GetFunction("OnLevelWasLoaded");
        CallMain();
    }

    protected void StartLooper()
    {
        loop = gameObject.AddComponent<LuaLooper>();
        loop.luaState = luaState;
    }

    protected virtual void Bind()
    {
        LuaBinder.Bind(luaState);
        LuaCoroutine.Register(luaState, this);
    }

    public static void AddSearchPath(LuaState luaState)
    {
        //从文件读入或是assetbundle
        if (!LuaFileUtils.Instance.beZip)
        {
            string[] toluaExtendArr = Directory.GetDirectories(Application.dataPath + "/toluaTool/Lua", "*", SearchOption.AllDirectories);
            string[] toluaArr = Directory.GetDirectories(Application.dataPath + "/ToLua/Lua", "*", SearchOption.AllDirectories);
            string[] luaScriptArr = Directory.GetDirectories(Application.dataPath + "/Lua", "*", SearchOption.AllDirectories);
            string[] gameScriptArr = Directory.GetDirectories(Application.dataPath + "/", "*", SearchOption.AllDirectories);
            //string[] zerobrandArr = Directory.GetDirectories(Application.dataPath + "\\ZeroBraneStudio", "*", SearchOption.AllDirectories);


            int length = 0; // toluaArr.GetLength(0);
            length = toluaExtendArr.Length;
            for (int i = 0; i < length; i++)
            {
                luaState.AddSearchPath(toluaExtendArr[i]);
            }
            length = toluaArr.GetLength(0);
            for (int i = 0; i < length; i++)
            {
                luaState.AddSearchPath(toluaArr[i]);
            }
            length = luaScriptArr.GetLength(0);
            for (int i = 0; i < length; i++)
            {
                luaState.AddSearchPath(luaScriptArr[i]);
            }
            length = gameScriptArr.GetLength(0);
            for (int i = 0; i < length; i++)
            {
                luaState.AddSearchPath(gameScriptArr[i]);
            }
        }
    }

    protected void Init()
    {
        InitLoader();
        luaState = new LuaState();
        OpenLibs();
        luaState.LuaSetTop(0);
        Bind();
        AddSearchPath(luaState);
        LoadLuaFiles();
    }

    protected void Awake()
    {
        Instance = this;
        Init();

#if UNITY_5_4
        SceneManager.sceneLoaded += OnSceneLoaded;
#endif
    }

    protected virtual void OnLoadFinished()
    {
        luaState.Start();
        StartLooper();
        StartMain();
    }

    void OnLevelLoaded(int level)
    {
        if (levelLoaded != null)
        {
            levelLoaded.BeginPCall();
            levelLoaded.Push(level);
            levelLoaded.PCall();
            levelLoaded.EndPCall();
        }
    }

#if UNITY_5_4
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        OnLevelLoaded(scene.buildIndex);
    }
#else
    protected void OnLevelWasLoaded(int level)
    {
        OnLevelLoaded(level);
    }
#endif

    public virtual void Destroy()
    {
        if (luaState != null)
        {
#if UNITY_5_4
        SceneManager.sceneLoaded -= OnSceneLoaded;
#endif
            LuaState state = luaState;
            luaState = null;

            if (levelLoaded != null)
            {
                levelLoaded.Dispose();
                levelLoaded = null;
            }

            if (loop != null)
            {
                loop.Destroy();
                loop = null;
            }

            state.Dispose();
            Instance = null;
        }
    }

    protected void OnDestroy()
    {
        Destroy();
    }

    protected void OnApplicationQuit()
    {
        Destroy();
    }

    public LuaLooper GetLooper()
    {
        return loop;
    }

    public static LuaState lua
    {
        get
        {
            return luaState;
        }
    }
}
