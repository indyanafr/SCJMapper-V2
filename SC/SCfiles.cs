﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using SCJMapper_V2.CryXMLlib;
using SCJMapper_V2.p4kFile;
using static SCJMapper_V2.SC.SCUiText;
using System.IO.Compression;

namespace SCJMapper_V2.SC
{
  /// <summary>
  /// Manages all SC files from Pak
  /// tracks the filedate to update only if needed
  /// </summary>
  class SCFiles
  {
    private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod( ).DeclaringType );

    private SCFile m_pakFile; // no content, carries only the filedate
    private SCFile m_defProfile;
    private Dictionary<string, SCFile> m_mapFiles;
    private Dictionary<string, SCFile> m_langFiles;

    // Singleton
    private static readonly Lazy<SCFiles> m_lazy = new Lazy<SCFiles>( () => new SCFiles( ) );
    public static SCFiles Instance { get => m_lazy.Value; }

    private SCFiles()
    {
      UpdatePack( );
    }


    public string DefaultProfile { get => m_defProfile.Filedata; }
    public IList<string> MapFiles
    {
      get => m_mapFiles.Keys.ToList( );
    }

    public string MapFile( string filekey )
    {
      if ( m_mapFiles.ContainsKey( filekey ) ) {
        return m_mapFiles[filekey].Filedata;
      }
      return "";
    }

    public IList<string> LangFiles
    {
      get => m_langFiles.Keys.ToList( );
    }
    public string LangFile( string filekey )
    {
      if ( m_langFiles.ContainsKey( filekey ) ) {
        return m_langFiles[filekey].Filedata;
      }
      return "";
    }

    /// <summary>
    /// Zip Extracts the file to a string
    /// SC Alpha 2.2: Have to find the new one in E:\G\StarCitizen\StarCitizen\LIVE\Data.p4k (contains the binary XML now)
    /// </summary>
    static private string ExtractDefaultBinProfileP4k( string defaultProfileName )
    {
      log.Debug( "ExtractDefaultBinProfileP4k - Entry" );

      string retVal = "";
      if ( File.Exists( SCPath.SCData_p4k ) ) {
        try {
          var PD = new p4kFile.p4kDirectory( );
          p4kFile.p4kFile p4K = PD.ScanDirectoryFor( SCPath.SCData_p4k, defaultProfileName );
          if ( p4K != null ) {
            byte[] fContent = PD.GetFile( SCPath.SCData_p4k, p4K );

            // use the binary XML reader
            CryXmlNodeRef ROOT = null;
            CryXmlBinReader.EResult readResult = CryXmlBinReader.EResult.Error;
            CryXmlBinReader cbr = new CryXmlBinReader( );
            ROOT = cbr.LoadFromBuffer( fContent, out readResult );
            if ( readResult == CryXmlBinReader.EResult.Success ) {
              XmlTree tree = new XmlTree( );
              tree.BuildXML( ROOT );
              retVal = tree.XML_string;
            }
            else {
              log.ErrorFormat( "  Error in CryXmlBinReader: {0}", cbr.GetErrorDescription( ) );
              retVal = ""; // clear any remanents
            }
          }
        }
        catch ( Exception ex ) {
          log.Error( "  Unexpected ", ex );
        }
      }
      return retVal;
    }

    /// <summary>
    /// Update from p4k (treats is like never read..)
    /// </summary>
    private void UpdatePakFile()
    {
      if ( File.Exists( SCPath.SCData_p4k ) ) {
        m_pakFile.Filetype = SCFile.FileType.PakFile;
        m_pakFile.Filename = Path.GetFileName( SCPath.SCData_p4k );
        m_pakFile.Filepath = Path.GetDirectoryName( SCPath.SCData_p4k );
        m_pakFile.FileDateTime = File.GetLastWriteTime( SCPath.SCData_p4k );
        m_pakFile.Filedata = "DUMMY CONTENT ONLY"; // not really used
      }
    }

    /// <summary>
    /// Update from the pack file (treats is like never read..)
    /// </summary>
    private void UpdateDefProfileFile()
    {
      log.Info( "UpdateDefProfileFile - Entry" );

      string retVal = "";
      if ( File.Exists( SCPath.SCData_p4k ) ) {
        try {
          var PD = new p4kFile.p4kDirectory( );
          p4kFile.p4kFile p4K = PD.ScanDirectoryFor( SCPath.SCData_p4k, SCDefaultProfile.DefaultProfileName );
          if ( p4K != null ) {
            byte[] fContent = PD.GetFile( SCPath.SCData_p4k, p4K );
            // use the binary XML reader
            CryXmlNodeRef ROOT = null;
            CryXmlBinReader.EResult readResult = CryXmlBinReader.EResult.Error;
            CryXmlBinReader cbr = new CryXmlBinReader( );
            ROOT = cbr.LoadFromBuffer( fContent, out readResult );
            if ( readResult == CryXmlBinReader.EResult.Success ) {
              XmlTree tree = new XmlTree( );
              tree.BuildXML( ROOT );
              retVal = tree.XML_string;
              // make our file - only this one gets a new one
              m_defProfile.Filetype = SCFile.FileType.DefProfile;
              m_defProfile.Filename = Path.GetFileName( p4K.Filename );
              m_defProfile.Filepath = Path.GetDirectoryName( p4K.Filename );
              m_defProfile.FileDateTime = p4K.FileModifyDate;
              m_defProfile.Filedata = retVal;
              log.Info( "UpdateDefProfileFile - read from pak file" );
            }
            else {
              log.ErrorFormat( "UpdateDefProfileFile - Error in CryXmlBinReader: {0}", cbr.GetErrorDescription( ) );
              retVal = ""; // clear any remanents
            }
          }
        }
        catch ( Exception ex ) {
          log.Error( "UpdateDefProfileFile - Unexpected ", ex );
        }

      }
    }

    /// <summary>
    /// Update all map files from the pak file (treats is like never read..)
    /// </summary>
    private void UpdateMapFiles()
    {
      log.Info( "UpdateMapFiles - Entry" );

      if ( File.Exists( SCPath.SCData_p4k ) ) {
        try {
          var PD = new p4kDirectory( );
          IList<p4kFile.p4kFile> fileList = PD.ScanDirectoryContaining( SCPath.SCData_p4k, @"Data\Libs\Config\Mappings\layout_" );

          foreach ( p4kFile.p4kFile file in fileList ) {
            byte[] fContent = PD.GetFile( SCPath.SCData_p4k, file );

            // use the binary XML reader
            CryXmlNodeRef ROOT = null;
            CryXmlBinReader.EResult readResult = CryXmlBinReader.EResult.Error;
            CryXmlBinReader cbr = new CryXmlBinReader( );
            ROOT = cbr.LoadFromBuffer( fContent, out readResult );
            if ( readResult == CryXmlBinReader.EResult.Success ) {
              XmlTree tree = new XmlTree( );
              tree.BuildXML( ROOT );
              // make our file - only this one gets a new one
              SCFile obj = new SCFile {
                Filetype = SCFile.FileType.MapFile,
                Filename = Path.GetFileName( file.Filename ),
                Filepath = Path.GetDirectoryName( file.Filename ),
                FileDateTime = file.FileModifyDate,
                Filedata = tree.XML_string
              };
              // replace
              if ( m_mapFiles.ContainsKey( obj.Filename ) )
                m_mapFiles.Remove( obj.Filename );
              m_mapFiles.Add( obj.Filename, obj );
              log.InfoFormat( "UpdateMapFiles - read from pak file {0}", obj.Filename );
            }
            else {
              log.ErrorFormat( "UpdateMapFiles - Error in CryXmlBinReader: {0}", cbr.GetErrorDescription( ) );
            }
          }
        }
        catch ( Exception ex ) {
          log.Error( "UpdateMapFiles - Unexpected ", ex );
        }
      }
    }

    /// <summary>
    /// Update all language files from the pak file (treats is like never read..)
    /// </summary>
    private void UpdateLangFiles()
    {
      log.Info( "UpdateLangFiles - Entry" );

      if ( File.Exists( SCPath.SCData_p4k ) ) {
        try {
          var PD = new p4kDirectory( );
          IList<p4kFile.p4kFile> fileList = PD.ScanDirectoryContaining( SCPath.SCData_p4k, @"\global.ini" );
          foreach ( p4kFile.p4kFile file in fileList ) {
            string retVal = "";
            string lang = Path.GetFileNameWithoutExtension( Path.GetDirectoryName( file.Filename ) );
            if ( Enum.TryParse( lang, out Languages fileLang ) ) {
              byte[] fContent = PD.GetFile( SCPath.SCData_p4k, file );
              using ( TextReader sr = new StringReader( Encoding.UTF8.GetString( fContent ) ) ) {
                string line = sr.ReadLine( );
                while ( line != null ) {
                  // try to get only valid lines
                  int epo = line.IndexOf( '=' );
                  string tag = "";
                  string content = "";
                  if ( epo >= 0 ) {
                    tag = line.Substring( 0, epo );
                    if ( line.Length >= ( epo + 1 ) ) {
                      content = line.Substring( epo + 1 );
                    }
                    if ( tag.StartsWith( "ui_C" ) ) {
                      // seems all strings we may need are ui_Cxyz
                      retVal += string.Format( "{0}\n", line );
                    }
                  }
                  line = sr.ReadLine( );
                }// while
              }//using
              // make our file - only this one gets a new one
              SCFile obj = new SCFile {
                Filetype = SCFile.FileType.LangFile,
                Filename = Path.GetFileName( lang.ToLowerInvariant( ) ), // all files are named global.ini so we take the directory name (language)
                Filepath = Path.GetDirectoryName( file.Filename ),
                FileDateTime = file.FileModifyDate,
                Filedata = retVal
              };
              // replace
              if ( m_langFiles.ContainsKey( obj.Filename ) )
                m_langFiles.Remove( obj.Filename );
              m_langFiles.Add( obj.Filename, obj );
              log.InfoFormat( "UpdateLangFiles - read from pak file {0}", obj.Filename );
            }

          }// all files
        }
        catch ( Exception ex ) {
          log.Error( "UpdateLangFiles - Unexpected ", ex );
        }
      }
    }


    /// <summary>
    /// Load all SC assets from the local stored files
    /// avoiding to read through the p4k files at each startup
    /// </summary>
    private void LoadPack()
    {
      // make sure we have valid but empty ones
      m_pakFile = new SCFile( );
      m_defProfile = new SCFile( );
      m_mapFiles = new Dictionary<string, SCFile>( );
      m_langFiles = new Dictionary<string, SCFile>( );

      if ( !Directory.Exists( TheUser.FileStoreDir ) )
        return; // EXIT - no files to read from - first time maybe or deleted

      // catch any serializing error
      try {
        IEnumerable<string> filelist = Directory.EnumerateFiles( TheUser.FileStoreDir, "*.scj" );
        foreach ( string file in filelist ) {
          SCFile obj = new SCFile( );

          using ( Stream stream = File.Open( file, FileMode.Open ) ) {
            using ( var gZipStream = new GZipStream( stream, CompressionMode.Decompress ) ) {
              BinaryFormatter binaryFormatter = new BinaryFormatter( );
              obj = (SCFile)binaryFormatter.Deserialize( gZipStream );
            }
            stream.Close( );
            if ( obj.Filetype == SCFile.FileType.PakFile ) {
              m_pakFile = obj;
            }
            else if ( obj.Filetype == SCFile.FileType.DefProfile ) {
              m_defProfile = obj;
            }
            else if ( obj.Filetype == SCFile.FileType.MapFile ) {
              m_mapFiles.Add( obj.Filename, obj );
            }
            else if ( obj.Filetype == SCFile.FileType.LangFile ) {
              m_langFiles.Add( obj.Filename, obj );
            }
          }
        }
      }
      catch ( Exception e ) {
        log.Error( "LoadPack - deserialization error:", e );
        return; // ERROR EXIT - cannot read
      }

    }

    /// <summary>
    /// Save all assets to a local store
    /// </summary>
    private void SavePack()
    {
      if ( m_pakFile.Filetype != SCFile.FileType.PakFile ) {
        log.Error( "SavePack - no valid data to save?? " );
        return; // nothing to save ??
      }

      // make sure we have a folder to write to
      try {
        if ( !Directory.Exists( TheUser.FileStoreDir ) )
          Directory.CreateDirectory( TheUser.FileStoreDir );
      }
      catch ( Exception e ) {
        log.Error( "SavePack - create dir error:", e );
        return; // ERROR EXIT - cannot create a dir to write to
      }

      // catch any serializing error
      try {
        // save p4k reference for the filedate
        using ( Stream stream = File.Open( Path.Combine( TheUser.FileStoreDir, m_pakFile.Filename + ".scj" ), FileMode.Create ) ) {
          using ( var gZipStream = new GZipStream( stream, CompressionMode.Compress ) ) {
            BinaryFormatter binaryFormatter = new BinaryFormatter( );
            binaryFormatter.Serialize( gZipStream, m_pakFile );
          }
          stream.Close( );
        }

        if ( m_defProfile.Filetype == SCFile.FileType.DefProfile ) {
          using ( Stream stream = File.Open( Path.Combine( TheUser.FileStoreDir, m_defProfile.Filename + ".scj" ), FileMode.Create ) ) {
            using ( var gZipStream = new GZipStream( stream, CompressionMode.Compress ) ) {
              BinaryFormatter binaryFormatter = new BinaryFormatter( );
              binaryFormatter.Serialize( gZipStream, m_defProfile );
            }
            stream.Close( );
          }
        }

        foreach ( KeyValuePair<string, SCFile> kv in m_mapFiles ) {
          if ( kv.Value.Filetype == SCFile.FileType.MapFile ) {
            using ( Stream stream = File.Open( Path.Combine( TheUser.FileStoreDir, kv.Value.Filename + ".scj" ), FileMode.Create ) ) {
              using ( var gZipStream = new GZipStream( stream, CompressionMode.Compress ) ) {
                BinaryFormatter binaryFormatter = new BinaryFormatter( );
                binaryFormatter.Serialize( gZipStream, kv.Value );
              }
              stream.Close( );
            }
          }
        }

        foreach ( KeyValuePair<string, SCFile> kv in m_langFiles ) {
          if ( kv.Value.Filetype == SCFile.FileType.LangFile ) {
            using ( Stream stream = File.Open( Path.Combine( TheUser.FileStoreDir, kv.Value.Filename + ".scj" ), FileMode.Create ) ) {
              using ( var gZipStream = new GZipStream( stream, CompressionMode.Compress ) ) {
                BinaryFormatter binaryFormatter = new BinaryFormatter( );
                binaryFormatter.Serialize( gZipStream, kv.Value );
              }
              stream.Close( );
            }
          }
        }
      }
      catch ( Exception e ) {
        log.Error( "SavePack - serialization error:", e );
        return; // ERROR EXIT - cannot write
      }
    }


    /// <summary>
    /// Checks if the p4k has changed since last read
    /// </summary>
    /// <returns>True if the p4k file is newer than the saved data</returns>
    private bool NeedsUpdate()
    {
      bool pakUpdated = true; // need to read
      // check pak and see if we had read it already
      if ( m_pakFile.Filetype == SCFile.FileType.PakFile ) {
        // seems we have read and saved some files at least once
        if ( File.Exists( SCPath.SCData_p4k ) ) {
          DateTime dateTime = File.GetLastWriteTime( SCPath.SCData_p4k );
          pakUpdated = ( dateTime > m_pakFile.FileDateTime );
        }
      }
      return pakUpdated;
    }

    /// <summary>
    /// Load the asset files from the local store and reloads from p4k if needed only
    /// </summary>
    private void UpdatePack()
    {
      LoadPack( ); // get the lastest ones
                   // either we have files or not...
      if ( NeedsUpdate( ) == false ) return; // EXIT without update

      UpdatePakFile( );
      UpdateDefProfileFile( );
      UpdateMapFiles( );
      UpdateLangFiles( );

      SavePack( ); // save the latest collection
    }




  }
}
