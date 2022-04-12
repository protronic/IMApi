﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Protronic.CeckedFileInfo;

#nullable disable

namespace IMApi.Migrations
{
    [DbContext(typeof(CheckedFileContext))]
    partial class CheckedFileContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "7.0.0-preview.2.22153.1");

            modelBuilder.Entity("Protronic.CeckedFileInfo.ConversionInfo", b =>
                {
                    b.Property<string>("ConversionName")
                        .HasColumnType("TEXT");

                    b.Property<string>("FileType")
                        .HasColumnType("TEXT");

                    b.Property<string>("OriginalFileFileName")
                        .HasColumnType("TEXT");

                    b.Property<int?>("Type")
                        .HasColumnType("INTEGER");

                    b.HasKey("ConversionName");

                    b.HasIndex("OriginalFileFileName");

                    b.ToTable("Conversions");
                });

            modelBuilder.Entity("Protronic.CeckedFileInfo.ConvertedFile", b =>
                {
                    b.Property<string>("WebURL")
                        .HasColumnType("TEXT");

                    b.Property<string>("ConversionName")
                        .HasColumnType("TEXT");

                    b.Property<uint>("FileCrc")
                        .HasColumnType("INTEGER");

                    b.Property<long>("FileLength")
                        .HasColumnType("INTEGER");

                    b.Property<string>("FileName")
                        .HasColumnType("TEXT");

                    b.Property<string>("FileType")
                        .HasColumnType("TEXT");

                    b.Property<string>("OriginalFileFileName")
                        .HasColumnType("TEXT");

                    b.HasKey("WebURL");

                    b.HasIndex("ConversionName");

                    b.HasIndex("OriginalFileFileName");

                    b.ToTable("ConvertedFiles");
                });

            modelBuilder.Entity("Protronic.CeckedFileInfo.OriginalFile", b =>
                {
                    b.Property<string>("FileName")
                        .HasColumnType("TEXT");

                    b.Property<string>("Artikelnummer")
                        .HasColumnType("TEXT");

                    b.Property<uint>("FileCrc")
                        .HasColumnType("INTEGER");

                    b.Property<long>("FileLength")
                        .HasColumnType("INTEGER");

                    b.Property<string>("FileType")
                        .HasColumnType("TEXT");

                    b.Property<string>("WebURL")
                        .HasColumnType("TEXT");

                    b.Property<int?>("lang")
                        .HasColumnType("INTEGER");

                    b.HasKey("FileName");

                    b.ToTable("OriginalFiles");
                });

            modelBuilder.Entity("Protronic.CeckedFileInfo.ConversionInfo", b =>
                {
                    b.HasOne("Protronic.CeckedFileInfo.OriginalFile", null)
                        .WithMany("conversions")
                        .HasForeignKey("OriginalFileFileName");
                });

            modelBuilder.Entity("Protronic.CeckedFileInfo.ConvertedFile", b =>
                {
                    b.HasOne("Protronic.CeckedFileInfo.ConversionInfo", "Conversion")
                        .WithMany()
                        .HasForeignKey("ConversionName");

                    b.HasOne("Protronic.CeckedFileInfo.OriginalFile", null)
                        .WithMany("convertedFiles")
                        .HasForeignKey("OriginalFileFileName");

                    b.Navigation("Conversion");
                });

            modelBuilder.Entity("Protronic.CeckedFileInfo.OriginalFile", b =>
                {
                    b.Navigation("conversions");

                    b.Navigation("convertedFiles");
                });
#pragma warning restore 612, 618
        }
    }
}
