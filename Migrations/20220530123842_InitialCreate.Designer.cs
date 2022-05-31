﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Protronic.CeckedFileInfo;

#nullable disable

namespace IMApi.Migrations
{
    [DbContext(typeof(CheckedFileContext))]
    [Migration("20220530123842_InitialCreate")]
    partial class InitialCreate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "7.0.0-preview.4.22229.2");

            modelBuilder.Entity("Protronic.CeckedFileInfo.ConversionInfo", b =>
                {
                    b.Property<string>("ConveretedFilePath")
                        .HasColumnType("TEXT");

                    b.Property<string>("BackgroundColor")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("ConversionName")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("FileType")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("Height")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Label")
                        .HasColumnType("TEXT");

                    b.Property<string>("OriginalFileFilePath")
                        .HasColumnType("TEXT");

                    b.Property<int>("Type")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Width")
                        .HasColumnType("INTEGER");

                    b.HasKey("ConveretedFilePath");

                    b.HasIndex("OriginalFileFilePath");

                    b.ToTable("Conversions");
                });

            modelBuilder.Entity("Protronic.CeckedFileInfo.ConvertedFile", b =>
                {
                    b.Property<string>("ConveretedFilePath")
                        .HasColumnType("TEXT");

                    b.Property<string>("ConversionConveretedFilePath")
                        .HasColumnType("TEXT");

                    b.Property<string>("FileMetaDataWebURL")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("OriginalFileFilePath")
                        .HasColumnType("TEXT");

                    b.HasKey("ConveretedFilePath");

                    b.HasIndex("ConversionConveretedFilePath");

                    b.HasIndex("FileMetaDataWebURL");

                    b.HasIndex("OriginalFileFilePath");

                    b.ToTable("ConvertedFiles");
                });

            modelBuilder.Entity("Protronic.CeckedFileInfo.FileMeta", b =>
                {
                    b.Property<string>("WebURL")
                        .HasColumnType("TEXT");

                    b.Property<string>("Artikelnummer")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<uint>("FileCrc")
                        .HasColumnType("INTEGER");

                    b.Property<long>("FileLength")
                        .HasColumnType("INTEGER");

                    b.Property<string>("FileName")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("FileType")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("lang")
                        .HasColumnType("INTEGER");

                    b.HasKey("WebURL");

                    b.ToTable("FileMeta");
                });

            modelBuilder.Entity("Protronic.CeckedFileInfo.OriginalFile", b =>
                {
                    b.Property<string>("FilePath")
                        .HasColumnType("TEXT");

                    b.Property<string>("FileMetaDataWebURL")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("FilePath");

                    b.HasIndex("FileMetaDataWebURL");

                    b.ToTable("OriginalFiles");
                });

            modelBuilder.Entity("Protronic.CeckedFileInfo.ConversionInfo", b =>
                {
                    b.HasOne("Protronic.CeckedFileInfo.OriginalFile", null)
                        .WithMany("Conversions")
                        .HasForeignKey("OriginalFileFilePath");
                });

            modelBuilder.Entity("Protronic.CeckedFileInfo.ConvertedFile", b =>
                {
                    b.HasOne("Protronic.CeckedFileInfo.ConversionInfo", "Conversion")
                        .WithMany()
                        .HasForeignKey("ConversionConveretedFilePath");

                    b.HasOne("Protronic.CeckedFileInfo.FileMeta", "FileMetaData")
                        .WithMany()
                        .HasForeignKey("FileMetaDataWebURL")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Protronic.CeckedFileInfo.OriginalFile", null)
                        .WithMany("ConvertedFiles")
                        .HasForeignKey("OriginalFileFilePath");

                    b.Navigation("Conversion");

                    b.Navigation("FileMetaData");
                });

            modelBuilder.Entity("Protronic.CeckedFileInfo.OriginalFile", b =>
                {
                    b.HasOne("Protronic.CeckedFileInfo.FileMeta", "FileMetaData")
                        .WithMany()
                        .HasForeignKey("FileMetaDataWebURL")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("FileMetaData");
                });

            modelBuilder.Entity("Protronic.CeckedFileInfo.OriginalFile", b =>
                {
                    b.Navigation("Conversions");

                    b.Navigation("ConvertedFiles");
                });
#pragma warning restore 612, 618
        }
    }
}