using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.EnterpriseServices;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OneC.ExternalComponents
{

    public delegate void InitEventHandler();
    public delegate void DoneEventHandler();

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class Export1cAttribute : Attribute
    {
        public readonly bool ValidOn;

        public Export1cAttribute()
        {
            ValidOn = true;
        }

        public Export1cAttribute(bool _validon)
        {
            ValidOn = _validon;
        }
    }

    [Guid("ab634004-f13d-11d0-a459-004095e1daea")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IAsyncEvent
    {
        /// Установка глубины буфера событий
        /// <param name="depth">Buffer depth</param>
        void SetEventBufferDepth(Int32 depth);

        /// Получение глубины буфера событий
        /// <param name="depth">Buffer depth</param>
        void GetEventBufferDepth(ref long depth);

        /// Посылка события
        /// <param name="source">Event source</param>
        /// <param name="message">Event message</param>
        /// <param name="data">Event data</param>
        void ExternalEvent(
            [MarshalAs(UnmanagedType.BStr)] String source,
            [MarshalAs(UnmanagedType.BStr)] String message,
            [MarshalAs(UnmanagedType.BStr)] String data
        );

        /// Очистка буфера событий
        void CleanBuffer();
    }

    [Guid("AB634005-F13D-11D0-A459-004095E1DAEA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IStatusLine
    {
        /// Задает текст статусной строки
        /// <param name="bstrStatusLine">Текст статусной строки</param>
        void SetStarusLine([MarshalAs(UnmanagedType.BStr)]String bstrStatusLine);

        /// Сброс статусной строки
        void ResetStatusLine();
    }

    [Guid("AB634001-F13D-11d0-A459-004095E1DAEA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IInitDone
    {
        void Init([MarshalAs(UnmanagedType.IDispatch)]object connection);

        void Done();

        void GetInfo([MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_VARIANT)]
                ref object[] info);
    }

    [Guid("AB634003-F13D-11d0-A459-004095E1DAEA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface ILanguageExtender
    {

        void RegisterExtensionAs([MarshalAs(UnmanagedType.BStr)]ref String extensionName);


        void GetNProps(ref Int32 props);


        void FindProp([MarshalAs(UnmanagedType.BStr)]String propName, ref Int32 propNum);


        void GetPropName(Int32 propNum, Int32 propAlias, [MarshalAs(UnmanagedType.BStr)]ref String propName);


        void GetPropVal(Int32 propNum, ref object propVal);


        void SetPropVal(Int32 propNum, ref object propVal);


        void IsPropReadable(Int32 propNum, ref bool propRead);


        void IsPropWritable(Int32 propNum, ref Boolean propWrite);


        void GetNMethods(ref Int32 pMethods);


        void FindMethod([MarshalAs(UnmanagedType.BStr)]String methodName, ref Int32 methodNum);


        void GetMethodName(Int32 methodNum, Int32 methodAlias, [MarshalAs(UnmanagedType.BStr)]
                            ref String methodName);


        void GetNParams(Int32 methodNum, ref Int32 pParams);

        void GetParamDefValue(Int32 methodNum, Int32 paramNum, ref object paramDefValue);


        void HasRetVal(Int32 methodNum, ref Boolean retValue);


        void CallAsProc(Int32 methodNum, [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_VARIANT)]
                        ref object[] pParams);


        void CallAsFunc(Int32 methodNum, ref object retValue,
                        [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_VARIANT)]
                        ref object[] pParams);
    }

    [ClassInterface(ClassInterfaceType.None)]
    public class ExtComponentBase : ServicedComponent, IInitDone, ILanguageExtender
    {
        private IAsyncEvent asyncevent;
        private IStatusLine statusline;
        private string compname;

        public event InitEventHandler InitEvent;
        public event DoneEventHandler DoneEvent;

        //таблица поиска номера метода по имени
        private Hashtable MethodNameToNumber;
        //таблица поиска имени метода по номеру
        private Hashtable MethodNumberToName;
        //таблица количества параметров у методов
        private Hashtable MethodNumberCountParams;
        //таблица поиска номера свойства по имени
        private Hashtable PropNameToNumber;
        //таблица поиска имени свойства по номеру
        private Hashtable PropNumberToName;
        //массив инфо о методах
        private MethodInfo[] MethodsInfo;
        //массив инфо о свойствах
        private PropertyInfo[] PropsInfo;

        //интерфейс вызова событий в 1с
        protected IAsyncEvent Async
        {
            get { return asyncevent; }
        }

        //интерфейс работы со строкой статуса
        protected IStatusLine StatusLine
        {
            get { return statusline; }
        }

        // имя компоненты при регистрации
        protected string ComponentName
        {
            get { return compname; }
            set { compname = value; }
        }

        //Инициализация компоненты
        void IInitDone.Init([MarshalAs(UnmanagedType.IDispatch)]
                        object connection)
        {
            MessageBox.Show("Ошибка", "Попытка деления на ноль", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);

            asyncevent = (IAsyncEvent)connection;
            statusline = (IStatusLine)connection;

            if (InitEvent != null)
            {
                InitEvent();
            }
        }

        void IInitDone.GetInfo([MarshalAs(UnmanagedType.SafeArray, SafeArraySubType=VarEnum.VT_VARIANT)]
                        ref object[] info)
        {
            info[0] = 2000;
        }

        //деинициализация компоненты
        void IInitDone.Done()
        {
            if (DoneEvent != null)
            {
                DoneEvent();
            }
        }

        //регистрация компоненты в 1с
        void ILanguageExtender.RegisterExtensionAs([MarshalAs(UnmanagedType.BStr)]
               ref String extensionName)
        {
            MethodNameToNumber = new Hashtable();
            MethodNumberToName = new Hashtable();
            MethodNumberCountParams = new Hashtable();

            PropNameToNumber = new Hashtable();
            PropNumberToName = new Hashtable();

            ArrayList TempMethodsInfo = new ArrayList();
            ArrayList TempPropsInfo = new ArrayList();
            foreach (MethodInfo m in this.GetType().GetMethods())
            {
                if ((m.DeclaringType == this.GetType()) &&
                    !(m.IsConstructor))
                {
                    object[] attrs = m.GetCustomAttributes(true);
                    foreach (object attr in attrs)
                    {
                        if ((attr is Export1cAttribute) & (((Export1cAttribute)attr).ValidOn))
                        {
                            int Identifier = TempMethodsInfo.Add(m);
                            MethodNameToNumber.Add(m.Name, Identifier);
                            MethodNumberToName.Add(Identifier, m.Name);
                            MethodNumberCountParams.Add(Identifier, m.GetParameters().Length);
                            break;
                        }
                    }
                }
            }
            foreach (PropertyInfo p in this.GetType().GetProperties())
            {
                if (p.DeclaringType == this.GetType())
                {
                    object[] attrs = p.GetCustomAttributes(true);
                    foreach (object attr in attrs)
                    {
                        if ((attr is Export1cAttribute) & (((Export1cAttribute)attr).ValidOn))
                        {
                            int Identifier = TempPropsInfo.Add(p);
                            PropNameToNumber.Add(p.Name, Identifier);
                            PropNumberToName.Add(Identifier, p.Name);
                            break;
                        }
                    }
                }

            }
            MethodsInfo = (MethodInfo[])TempMethodsInfo.ToArray(typeof(MethodInfo));
            PropsInfo = (PropertyInfo[])TempPropsInfo.ToArray(typeof(PropertyInfo));
            if (compname != null)
            {
                extensionName = compname;
            }
            else
            {
                throw new Exception("Не указано имя компоненты");
            }
        }

        //Возвращает количество свойств
        void ILanguageExtender.GetNProps(ref Int32 props)
        {
            props = PropsInfo.Length;
        }

        //Возвращает целочисленный идентификатор свойства, соответствующий 
        // переданному имени
        void ILanguageExtender.FindProp([MarshalAs(UnmanagedType.BStr)]String propName,
                            ref Int32 propNum)
        {
            propNum = (int)PropNameToNumber[propName];
            if (propNum == 0)
            {
                throw new Exception("Свойство с именем " + propName + " не найдено");
            }
        }

        //Возвращает имя свойства, соответствующее
        //переданному целочисленному идентификатору
        void ILanguageExtender.GetPropName(Int32 propNum, Int32 propAlias,
                                [MarshalAs(UnmanagedType.BStr)]ref String propName)
        {
            propName = (string)PropNumberToName[propNum];
            if (propName == null)
            {
                throw new Exception("Ошибка свойство не найдено");
            }
        }

        //Возвращает значение свойства.
        void ILanguageExtender.GetPropVal(Int32 propNum, ref object propVal)
        {
            propVal = PropsInfo[propNum].GetValue(this, null);
        }

        //Устанавливает значение свойства.
        void ILanguageExtender.SetPropVal(Int32 propNum, ref object propVal)
        {
            PropsInfo[propNum].SetValue(this, propVal, null);
        }

        //Определяет, можно ли читать значение свойства.
        void ILanguageExtender.IsPropReadable(Int32 propNum, ref bool propRead)
        {
            propRead = PropsInfo[propNum].CanRead;
        }

        //Определяет, можно ли изменять значение свойства
        void ILanguageExtender.IsPropWritable(Int32 propNum, ref Boolean propWrite)
        {
            propWrite = PropsInfo[propNum].CanWrite;
        }

        //Возвращает количество методов
        void ILanguageExtender.GetNMethods(ref Int32 pMethods)
        {
            pMethods = MethodsInfo.Length;
        }

        // Возвращает идентификатор метода по его имени
        void ILanguageExtender.FindMethod([MarshalAs(UnmanagedType.BStr)]String methodName,
                                ref Int32 methodNum)
        {
            methodNum = (int)MethodNameToNumber[methodName];
            if (methodNum == 0)
            {
                throw new Exception("Метод с именем " + methodName + " не найден");
            }
        }

        // Возвращает имя метода по его идентификатору
        void ILanguageExtender.GetMethodName(Int32 methodNum, Int32 methodAlias,
                                        [MarshalAs(UnmanagedType.BStr)]ref String methodName)
        {
            methodName = (string)MethodNumberToName[methodNum];
            if (methodName == null)
            {
                throw new Exception("Ошибка метод не найден");
            }
        }

        // Возвращает количество параметров метода по его идентификатору
        void ILanguageExtender.GetNParams(Int32 methodNum, ref Int32 pParams)
        {
            pParams = (int)MethodNumberCountParams[methodNum];
        }

        //Возвращает значение параметра метода поумолчанию
        void ILanguageExtender.GetParamDefValue(Int32 methodNum, Int32 paramNum, ref object paramDefValue)
        {
            paramDefValue = MethodsInfo[methodNum].GetParameters()[paramNum].DefaultValue;
        }

        //Указывает, что у метода есть возвращаемое значение
        void ILanguageExtender.HasRetVal(Int32 methodNum, ref Boolean retValue)
        {
            if (MethodsInfo[methodNum].ReturnType == typeof(void))
            {
                retValue = false;
            }
            else
            {
                retValue = true;
            }
        }

        // Вызов метода как процедуры с использованием идентификатора
        void ILanguageExtender.CallAsFunc(Int32 methodNum, ref object retValue,
                                [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType=VarEnum.VT_VARIANT)]
                                   ref object[] pParams)
        {
            retValue = MethodsInfo[methodNum].Invoke(this, pParams);
        }

        // Вызов метода как функции с использованием идентификатора
        void ILanguageExtender.CallAsProc(Int32 methodNum, [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType=VarEnum.VT_VARIANT)]
                                ref object[] pParams)
        {
            MethodsInfo[methodNum].Invoke(this, pParams);
        }

    }
}
