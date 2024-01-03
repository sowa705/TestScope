using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;
using System.Drawing;
using Silk.NET.Maths;
using Window = Silk.NET.Windowing.Window;

namespace TestScope
{
    class Program
    {
        private static IWindow window;
        private static GL Gl;

        private static uint Shader;
        private static uint Vao;
        private static uint GraphicsBuffer;

        //Vertex shaders are run on each vertex.
        private static readonly string VertexShaderSource = @"
        #version 430
                
        layout(std430, binding = 0) buffer layoutName
        {
            float data[];
        };

        out float length;

        void main()
        {
            float x = 1.0 / 400 * gl_VertexID * 2 - 1.0;
            gl_Position = vec4(x, data[gl_VertexID]*0.5, 0.0, 1.0);

            length = abs(data[gl_VertexID] - data[gl_VertexID+1]) + abs(data[gl_VertexID+1] - data[gl_VertexID+2])+abs(data[gl_VertexID+2] - data[gl_VertexID+3]);
        }
        ";

        //Fragment shaders are run on each fragment/pixel of the geometry.
        private static readonly string FragmentShaderSource = @"
        #version 430
        out vec4 FragColor;

        in float length;

        void main()
        {
            FragColor = vec4(1.0f, 1.0f, 0f, (1/length)*1.0/120.0);
        }
        ";

        private static void Main(string[] args)
        {
            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(1280, 720);
            options.Title = "Digital phosphor";
            options.Samples = 1;
            window = Window.Create(options);

            inputBuffer = new float[1024*1024*2];

            for (int i = 0; i < inputBuffer.Length; i++)
            {
                inputBuffer[i] = (float) (Math.Sin(i / 41f*Math.PI*2)*Math.Sin(i / 200.0*Math.PI*2)) + (Random.Shared.NextSingle()-0.5f)*0.05f;
            }

            window.Load += OnLoad;
            window.Render += OnRender;
            window.Update += OnUpdate;
            window.Closing += OnClose;

            window.Run();

            window.Dispose();
        }

        private static float[] inputBuffer;


        private static unsafe void OnLoad()
        {
            IInputContext input = window.CreateInput();
            for (int i = 0; i < input.Keyboards.Count; i++)
            {
                input.Keyboards[i].KeyDown += KeyDown;
            }

            //Getting the opengl api for drawing to the screen.
            Gl = GL.GetApi(window);

            //Creating a vertex shader.
            uint vertexShader = Gl.CreateShader(ShaderType.VertexShader);
            Gl.ShaderSource(vertexShader, VertexShaderSource);
            Gl.CompileShader(vertexShader);

            //Checking the shader for compilation errors.
            string infoLog = Gl.GetShaderInfoLog(vertexShader);
            if (!string.IsNullOrWhiteSpace(infoLog))
            {
                Console.WriteLine($"Error compiling vertex shader {infoLog}");
            }

            //Creating a fragment shader.
            uint fragmentShader = Gl.CreateShader(ShaderType.FragmentShader);
            Gl.ShaderSource(fragmentShader, FragmentShaderSource);
            Gl.CompileShader(fragmentShader);

            //Checking the shader for compilation errors.
            infoLog = Gl.GetShaderInfoLog(fragmentShader);
            if (!string.IsNullOrWhiteSpace(infoLog))
            {
                Console.WriteLine($"Error compiling fragment shader {infoLog}");
            }

            //Combining the shaders under one shader program.
            Shader = Gl.CreateProgram();
            Gl.AttachShader(Shader, vertexShader);
            Gl.AttachShader(Shader, fragmentShader);
            Gl.LinkProgram(Shader);

            //Checking the linking for errors.
            Gl.GetProgram(Shader, GLEnum.LinkStatus, out var status);
            if (status == 0)
            {
                Console.WriteLine($"Error linking shader {Gl.GetProgramInfoLog(Shader)}");
            }

            //Delete the no longer useful individual shaders;
            Gl.DetachShader(Shader, vertexShader);
            Gl.DetachShader(Shader, fragmentShader);
            Gl.DeleteShader(vertexShader);
            Gl.DeleteShader(fragmentShader);
            
            // create graphics buffer

            GraphicsBuffer = Gl.GenBuffer();
            Gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, GraphicsBuffer);
            float[] data = new float[400];
            fixed (float* d = data)
            {
                Gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (uint) (data.Length * sizeof(float)), d, BufferUsageARB.StaticDraw);
            }
            
            // empty vao
            Vao = Gl.GenVertexArray();
            Gl.BindVertexArray(Vao);
            
            //Gl.ClearColor(Color.DarkBlue);
            Gl.LineWidth(3);
            Gl.Enable(GLEnum.Blend);
            Gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
        }
        
        static int iters = 0;

        private static unsafe void OnRender(double obj) //Method needs to be unsafe due to draw elements.
        {
            iters++;
            //Clear the color channel.
            Gl.Clear((uint) ClearBufferMask.ColorBufferBit);
            
            float[] sampleMemory = new float[400];
            
            int bases = Random.Shared.Next(0, 800) * 400;

            for (int i = 0; i < 500; i++)
            {
                Array.Copy(inputBuffer, bases+400*i, sampleMemory, 0, 400);
            
                Gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, GraphicsBuffer);
                fixed (float* d = sampleMemory)
                {
                    Gl.BufferSubData(BufferTargetARB.ShaderStorageBuffer, 0, (uint) (sampleMemory.Length * sizeof(float)), d);
                }

                //Bind the geometry and shader.
                Gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 0, GraphicsBuffer);
                Gl.UseProgram(Shader);
                Gl.BindVertexArray(Vao);

                //Draw the geometry.
                Gl.DrawArrays(PrimitiveType.LineStrip,0, 397);
            }
        }

        private static void OnUpdate(double obj)
        {

        }

        private static void OnClose()
        {
            //Remember to delete the buffers.
            Gl.DeleteBuffer(GraphicsBuffer);
            Gl.DeleteProgram(Shader);
        }

        private static void KeyDown(IKeyboard arg1, Key arg2, int arg3)
        {
            if (arg2 == Key.Escape)
            {
                window.Close();
            }
        }
    }
}