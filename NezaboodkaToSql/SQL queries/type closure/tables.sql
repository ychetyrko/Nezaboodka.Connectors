/*======================================

		Nezaboodka database template

======================================*/

DROP DATABASE IF EXISTS `nz_test_closure`;
CREATE DATABASE `nz_test_closure` DEFAULT CHARSET=`UTF8` COLLATE `UTF8_BIN`;

USE `nz_test_closure`;

/*---------------------------------------/
			Schema Info
--------------------------------------*/

CREATE TABLE `nz_test_closure`.`type`(
	`id` INT PRIMARY KEY NOT NULL AUTO_INCREMENT,
	`name` VARCHAR(128) NOT NULL UNIQUE
		CHECK(`name` != ''),
	`table_name` VARCHAR(64) NOT NULL UNIQUE COLLATE `UTF8_GENERAL_CI`
		CHECK(`table_name` != ''),
	`base_type_name` VARCHAR(128)
		CHECK(`base_type_name` != '')
) ENGINE=`INNODB`;


CREATE TABLE `nz_test_closure`.`type_closure`(
	`ancestor` INT NOT NULL,
	`descendant` INT NOT NULL,
	
	FOREIGN KEY(`ancestor`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE,
	
	FOREIGN KEY(`descendant`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE,
	
	CONSTRAINT `uc_keys`
		UNIQUE (`ancestor`, `descendant`)
) ENGINE=`INNODB`;


CREATE TABLE `nz_test_closure`.`field` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`owner_type_name` VARCHAR(128) NOT NULL
		CHECK(`owner_type_name` != ''),
	`owner_type_id` INT DEFAULT NULL,
	`name` VARCHAR(128) NOT NULL
		CHECK(`name` != ''),
	`col_name` VARCHAR(64) NOT NULL COLLATE `UTF8_GENERAL_CI`
		CHECK(`col_name` != ''),
	`type_name` VARCHAR(64) NOT NULL
		CHECK(`type_name` != ''),
	`ref_type_id` INT DEFAULT NULL,
	`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
	`compare_options` ENUM (
		'None',
		'IgnoreCase',
		'IgnoreNonSpace',
		'IgnoreSymbols',
		'IgnoreKanaType',
		'IgnoreWidth',
		'OrdinalIgnoreCase',
		'StringSort',
		'Ordinal'
	) NOT NULL DEFAULT 'None',
	`back_ref_name` VARCHAR(128) DEFAULT NULL
		CHECK(`back_ref_name` != ''),
	`back_ref_id` INT DEFAULT NULL,
	
	FOREIGN KEY(`owner_type_id`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE,
	
	FOREIGN KEY(`ref_type_id`)
		REFERENCES `type`(`id`)
		ON DELETE RESTRICT,	-- to prevent deletion of referenced type
	
	FOREIGN KEY(`back_ref_id`)
		REFERENCES `field`(`id`)
		ON DELETE SET NULL,

	CONSTRAINT `uc_type_fields`
		UNIQUE (`owner_type_name`, `name`),

	CONSTRAINT `uc_table_columns`
		UNIQUE (`owner_type_name`, `col_name`)
) ENGINE=`INNODB`;

/*---------------------------------------/
			Default Structure
--------------------------------------*/

CREATE TABLE `nz_test_closure`.`db_key` (
	`sys_id` BIGINT(0) PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`rev_flags` BIGINT(0) NOT NULL DEFAULT 1,
	`real_type_id` INT NOT NULL,
	
	FOREIGN KEY (`real_type_id`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE	-- delete key when it's type info is removed
) ENGINE=`INNODB`;


CREATE TABLE `nz_test_closure`.`list` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`owner_id` BIGINT(0) NOT NULL,
	`type_id` INT NOT NULL,
    `field_id` INT NOT NULL,
	
	FOREIGN KEY (`owner_id`)
		REFERENCES `db_key`(`sys_id`)
		ON DELETE CASCADE,	-- cleanup list of deleted object
	
	FOREIGN KEY (`type_id`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE,	-- delete list when it's type info is removed
	
	FOREIGN KEY (`field_id`)
		REFERENCES `field`(`id`)
		ON DELETE CASCADE	-- delete list when it's field info is removed
) ENGINE=`INNODB`;


CREATE TABLE `nz_test_closure`.`list_item` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`list_id` INT NOT NULL,
	`ref` BIGINT(0) NOT NULL,
	
	FOREIGN KEY (`list_id`)
		REFERENCES `list`(`id`)
		ON DELETE CASCADE,	-- clear removed lists
	
	FOREIGN KEY (`ref`)
		REFERENCES `db_key`(`sys_id`)
		ON DELETE CASCADE	-- remove deleted object from all lists
) ENGINE=`INNODB`;
